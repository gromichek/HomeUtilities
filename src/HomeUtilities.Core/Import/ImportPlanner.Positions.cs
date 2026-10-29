using System.Globalization;
using HomeUtilities.Core.Consumption;
using HomeUtilities.Core.Domain;

namespace HomeUtilities.Core.Import;

public static partial class ImportPlanner
{
    private const string EstimatedNote = "estimated during import";

    // A position is a replacement chain of one register: the meters that replaced one another, seen as one meter over time.
    private sealed class Position(RegisterColumnMapping mapping, Guid key, RegisterKind kind, EnergyDirection direction, string? name)
    {
        public RegisterColumnMapping Mapping { get; } = mapping;

        public Guid Key { get; } = key;

        public RegisterKind Kind { get; } = kind;

        public EnergyDirection Direction { get; } = direction;

        public string? Name { get; } = name;

        public List<Segment> Segments { get; } = [];

        public List<ReadingCandidate> Candidates { get; } = [];
    }

    // One meter of a position with the dates it was in use; RemovedOn also reflects replacements planned by this import.
    private sealed class Segment(Meter meter, MeterRegister register, bool isPlanned)
    {
        public Meter Meter { get; } = meter;

        public MeterRegister Register { get; } = register;

        public bool IsPlanned { get; } = isPlanned;

        public DateOnly InstalledOn { get; } = meter.InstalledOn;

        public DateOnly? RemovedOn { get; set; } = meter.RemovedOn;
    }

    private sealed class ReadingCandidate(PlannedRow row, Position position, string column, DateOnly date, decimal value)
    {
        public PlannedRow Row { get; } = row;

        public Position Position { get; } = position;

        public string Column { get; } = column;

        public DateOnly Date { get; } = date;

        public decimal Value { get; } = value;

        public bool Accepted { get; set; }

        public string? NoteToAttach { get; set; }

        public decimal? PreviousValue { get; set; }

        public decimal PreviousStep { get; set; }

        public PlannedReplacementRegister? Line { get; set; }

        public Segment? Segment { get; set; }

        public bool Skipped { get; set; }
    }

    // A replacement whose final value waits until all positions of the row are known.
    private sealed record EstimateDraft(ReadingCandidate Candidate, PlannedReplacementRegister Line, decimal PreviousValue);

    private static List<Position> BuildPositions(
        ImportProfile profile,
        ImportContext context,
        Dictionary<Guid, (Meter Meter, MeterRegister Register)> registers)
    {
        var positions = new List<Position>();
        foreach (var mapping in profile.Registers.Where(r => r.ReadingColumn > 0))
        {
            var (meter, register) = registers[mapping.RegisterId];
            var segments = new List<Segment>();
            foreach (var chainMeter in MeterChain.Build(context.Meters, meter.Id).Meters)
            {
                var chainRegister = chainMeter.Registers.FirstOrDefault(r => !r.IsDeleted && r.Kind == register.Kind && r.Direction == register.Direction && r.Name == register.Name)
                    ?? chainMeter.Registers.FirstOrDefault(r => !r.IsDeleted && r.Kind == register.Kind && r.Direction == register.Direction);
                if (chainRegister is not null)
                {
                    segments.Add(new Segment(chainMeter, chainRegister, false));
                }
            }

            // The position is named after the register of the first meter, so a profile made with any meter of the chain works.
            if (segments.Count > 0 && positions.All(p => p.Key != segments[0].Register.Id))
            {
                var position = new Position(mapping, segments[0].Register.Id, register.Kind, register.Direction, register.Name);
                position.Segments.AddRange(segments);
                positions.Add(position);
            }
        }

        return positions;
    }

    private static Segment? SegmentAt(Position position, DateOnly date) =>
        position.Segments
            .Where(s => s.InstalledOn <= date && (s.RemovedOn is null || date <= s.RemovedOn))
            .OrderByDescending(s => s.InstalledOn)
            .FirstOrDefault();

    // Finds replacements in the readings of every position, creates the new meters and assigns every reading to the meter in place on its date.
    private static List<PlannedReplacement> PlanReadings(
        ImportProfile profile,
        SheetData sheet,
        ImportContext context,
        List<Position> positions,
        List<ReadingCandidate> allCandidates,
        List<PlannedRow> rows)
    {
        var replacements = new List<PlannedReplacement>();
        var drafts = new List<EstimateDraft>();
        var edits = context.ReplacementEdits ?? [];

        foreach (var position in positions)
        {
            DetectAndPlan(profile, sheet, context, positions, position, replacements, drafts, edits);
        }

        EstimateFinals(profile, sheet, drafts, allCandidates);
        AssignAndValidate(positions, replacements);
        return [.. replacements.OrderBy(r => r.Date).ThenBy(r => r.RowNumber)];
    }

    private static void DetectAndPlan(
        ImportProfile profile,
        SheetData sheet,
        ImportContext context,
        List<Position> positions,
        Position position,
        List<PlannedReplacement> replacements,
        List<EstimateDraft> drafts,
        IReadOnlyList<ReplacementEdit> edits)
    {
        var series = StoredPoints(position);
        foreach (var candidate in position.Candidates.Where(c => !c.Row.HasErrors).OrderBy(c => c.Date).ThenBy(c => c.Row.RowNumber))
        {
            if (series.Any(p => p.Date == candidate.Date))
            {
                candidate.Skipped = true;
                Warning(candidate.Row, ImportIssueCodes.ReadingExists, candidate.Column, candidate.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
                continue;
            }

            var history = series.Where(p => p.Date < candidate.Date).ToList();
            var point = new ReadingPoint(candidate.Date, candidate.Value, candidate.Row.RowNumber);
            if (history.Count > 0)
            {
                var previous = history[^1];
                candidate.PreviousValue = previous.Value;
                candidate.PreviousStep = history.Count > 1 ? Math.Max(0, previous.Value - history[^2].Value) : 0;
                if (profile.DetectReplacements
                    && ReplacementDetector.Detect(history, point, SegmentAt(position, previous.Date)?.Meter.Digits) is { } found
                    && SegmentAt(position, previous.Date) is { } oldSegment
                    && (oldSegment == position.Segments[^1] || replacements.Any(r => r.OldMeterId == oldSegment.Meter.Id && r.RowNumber == candidate.Row.RowNumber))
                    && (oldSegment.RemovedOn is null || replacements.Any(r => r.OldMeterId == oldSegment.Meter.Id && r.RowNumber == candidate.Row.RowNumber)))
                {
                    PlanReplacement(profile, sheet, context, positions, position, oldSegment, candidate, previous, found, replacements, drafts, edits);
                }
            }

            var index = series.FindIndex(p => p.Date > candidate.Date);
            series.Insert(index >= 0 ? index : series.Count, point);
        }
    }

    private static List<ReadingPoint> StoredPoints(Position position) =>
    [
        .. position.Segments
            .SelectMany((s, order) => s.Register.Readings.Where(r => !r.IsDeleted).Select(r => (Reading: r, Order: order)))
            .OrderBy(x => x.Reading.Date)
            .ThenBy(x => x.Order)
            .ThenBy(x => x.Reading.CreatedAt)
            .Select(x => new ReadingPoint(x.Reading.Date, x.Reading.Value, 0)),
    ];

    private static void PlanReplacement(
        ImportProfile profile,
        SheetData sheet,
        ImportContext context,
        List<Position> positions,
        Position position,
        Segment oldSegment,
        ReadingCandidate candidate,
        ReadingPoint previous,
        DetectedReplacement found,
        List<PlannedReplacement> replacements,
        List<EstimateDraft> drafts,
        IReadOnlyList<ReplacementEdit> edits)
    {
        var edit = edits.FirstOrDefault(e => e.RowNumber == candidate.Row.RowNumber && e.PositionRegisterId == position.Key);
        var note = FindNote(profile, sheet, candidate.Row.RowNumber);
        var entry = PickEntry(note, oldSegment.Meter, positions, position, candidate.Row.RowNumber);

        var date = edit?.Date;
        if (date is null)
        {
            if (found.InstallationDayRow)
            {
                date = candidate.Date;
            }
            else
            {
                date = note.Date is { } noted && noted > previous.Date && noted <= candidate.Date ? noted : previous.Date.AddDays(1);
            }
        }

        var replacementDate = date.Value < previous.Date.AddDays(1) ? previous.Date.AddDays(1) : date.Value > candidate.Date ? candidate.Date : date.Value;

        var replacement = replacements.FirstOrDefault(r => r.OldMeterId == oldSegment.Meter.Id && r.RowNumber == candidate.Row.RowNumber);
        if (replacement is null)
        {
            replacement = CreateReplacement(context, replacements, oldSegment, replacementDate, candidate.Row.RowNumber, entry?.SerialNumber);
            replacements.Add(replacement);
            foreach (var other in positions.Where(p => p.Segments[^1].Meter.Id == oldSegment.Meter.Id))
            {
                var otherOld = other.Segments[^1];
                var newRegister = replacement.NewMeter.Registers.First(r => r.Kind == otherOld.Register.Kind && r.Direction == otherOld.Register.Direction && r.Name == otherOld.Register.Name);
                otherOld.RemovedOn = replacementDate;
                if (otherOld.IsPlanned)
                {
                    // A meter created by this import and replaced by it again: the chain keeps its dates.
                    otherOld.Meter.RemovedOn = replacementDate;
                }

                other.Segments.Add(new Segment(replacement.NewMeter, newRegister, true));
            }
        }
        else
        {
            // Another register of the same meter was replaced in this row: it shares the date.
            replacementDate = replacement.Date;
        }

        if (edit?.SerialNumber is { } serial && serial.Length > 0)
        {
            replacement.NewMeter.SerialNumber = serial;
        }

        var line = replacement.Registers.First(r => r.PositionRegisterId == Guid.Empty && r.OldRegisterId == oldSegment.Register.Id);
        var rowInitial = replacementDate == candidate.Date;
        decimal initial;
        var initialEstimated = false;
        if (edit?.Initial is { } editedInitial)
        {
            initial = editedInitial;
        }
        else if (rowInitial)
        {
            initial = candidate.Value;
        }
        else if (entry?.NewInitial is { } noted)
        {
            initial = noted;
        }
        else
        {
            // Without a value from the sheet the new meter is assumed to start at 0; a jump up means it started near the value read.
            initial = found.Sign == ReplacementSign.Jump ? candidate.Value : 0m;
            initialEstimated = true;
        }

        var final = previous.Value;
        var finalEstimated = false;
        var needsEstimate = false;
        if (edit?.Final is { } editedFinal)
        {
            final = editedFinal;
        }
        else if (rowInitial)
        {
            // The installation-day row: the old meter's last reading is the last value before.
            final = previous.Value;
        }
        else if (entry?.OldFinal is { } notedFinal)
        {
            final = notedFinal;
        }
        else
        {
            finalEstimated = true;
            needsEstimate = true;
        }

        var replaced = ReplaceLine(replacement, line, position.Key, replacementDate, final, finalEstimated, initial, initialEstimated, rowInitial);
        candidate.Line = replaced;
        if (needsEstimate)
        {
            drafts.Add(new EstimateDraft(candidate, replaced, previous.Value));
        }

        Info(candidate.Row, ImportIssueCodes.ReplacementProposed);
    }

    // Replaces the placeholder line of the register by one with the values of the sheet.
    private static PlannedReplacementRegister ReplaceLine(
        PlannedReplacement replacement,
        PlannedReplacementRegister placeholder,
        Guid positionKey,
        DateOnly date,
        decimal final,
        bool finalEstimated,
        decimal initial,
        bool initialEstimated,
        bool rowInitial)
    {
        var index = replacement.Registers.IndexOf(placeholder);
        var line = new PlannedReplacementRegister
        {
            PositionRegisterId = positionKey,
            OldRegisterId = placeholder.OldRegisterId,
            NewRegisterId = placeholder.NewRegisterId,
            FinalReading = placeholder.FinalReading,
            InitialReading = placeholder.InitialReading,
            InitialIsRowReading = rowInitial,
            FinalEstimated = finalEstimated,
            InitialEstimated = initialEstimated,
        };
        line.FinalReading.Value = final;
        line.FinalReading.Kind = finalEstimated ? ReadingKind.Estimated : ReadingKind.Final;
        line.FinalReading.Note = finalEstimated ? EstimatedNote : null;
        line.InitialReading.Value = initial;
        line.InitialReading.Note = initialEstimated ? EstimatedNote : null;
        replacement.Registers[index] = line;
        foreach (var other in replacement.Registers)
        {
            other.FinalReading.Date = date;
            other.InitialReading.Date = date;
        }

        replacement.Date = date;
        replacement.NewMeter.InstalledOn = date;
        return line;
    }

    private static PlannedReplacement CreateReplacement(ImportContext context, List<PlannedReplacement> replacements, Segment oldSegment, DateOnly date, int rowNumber, string? serial)
    {
        var old = oldSegment.Meter;
        var newMeter = new Meter
        {
            PropertyId = old.PropertyId,
            Resource = old.Resource,
            Unit = old.Unit,
            Location = old.Location,
            Digits = old.Digits,
            SerialNumber = serial,
            InstalledOn = date,
            ReplacedMeterId = old.Id,
        };

        var replacement = new PlannedReplacement
        {
            OldMeterId = old.Id,
            OldMeterIsPlanned = oldSegment.IsPlanned,
            NewMeter = newMeter,
            Date = date,
            RowNumber = rowNumber,
        };

        foreach (var oldRegister in old.Registers.Where(r => !r.IsDeleted))
        {
            var newRegister = new MeterRegister { MeterId = newMeter.Id, Kind = oldRegister.Kind, Direction = oldRegister.Direction, Name = oldRegister.Name };
            newMeter.Registers.Add(newRegister);

            // Registers of the meter that the sheet does not read: the last known value and 0, both marked as estimated.
            var lastKnown = oldRegister.Readings.Where(r => !r.IsDeleted && r.Date <= date).OrderByDescending(r => r.Date).FirstOrDefault()?.Value ?? 0m;
            replacement.Registers.Add(new PlannedReplacementRegister
            {
                PositionRegisterId = Guid.Empty,
                OldRegisterId = oldRegister.Id,
                NewRegisterId = newRegister.Id,
                FinalReading = new MeterReading { RegisterId = oldRegister.Id, Date = date, Value = lastKnown, Kind = ReadingKind.Estimated, Note = EstimatedNote },
                InitialReading = new MeterReading { RegisterId = newRegister.Id, Date = date, Value = 0m, Kind = ReadingKind.Initial, Note = EstimatedNote },
                FinalEstimated = true,
                InitialEstimated = true,
                Unmapped = true,
            });
        }

        AddLinks(context, replacements, replacement, old, date);
        return replacement;
    }

    private static void AddLinks(ImportContext context, List<PlannedReplacement> replacements, PlannedReplacement replacement, Meter old, DateOnly date)
    {
        var end = date.AddDays(-1);
        var previous = replacements.FirstOrDefault(r => r.NewMeter.Id == old.Id);
        if (previous is not null)
        {
            // The old meter is itself new in this import: its links are the ones the earlier replacement created.
            foreach (var link in previous.LinksToCreate)
            {
                link.ValidTo = end < link.ValidFrom ? link.ValidFrom : end;
                replacement.LinksToCreate.Add(new ContractMeter { ContractId = link.ContractId, MeterId = replacement.NewMeter.Id, ValidFrom = date });
            }

            return;
        }

        if (context.ContractLinks is null)
        {
            replacement.LinksToCreate.Add(new ContractMeter { ContractId = context.Contract.Id, MeterId = replacement.NewMeter.Id, ValidFrom = date });
            return;
        }

        foreach (var link in context.ContractLinks.Where(l => l.MeterId == old.Id && !l.IsDeleted && (l.ValidTo is null || l.ValidTo >= date)))
        {
            replacement.LinksToClose.Add(new ContractMeter
            {
                Id = link.Id,
                CreatedAt = link.CreatedAt,
                UpdatedAt = link.UpdatedAt,
                ContractId = link.ContractId,
                MeterId = link.MeterId,
                ValidFrom = link.ValidFrom,
                ValidTo = end < link.ValidFrom ? link.ValidFrom : end,
            });
            replacement.LinksToCreate.Add(new ContractMeter { ContractId = link.ContractId, MeterId = replacement.NewMeter.Id, ValidFrom = date });
        }
    }

    private static ReplacementNote FindNote(ImportProfile profile, SheetData sheet, int rowNumber)
    {
        // The note may sit on the row itself or on a neighbour: the row, the one below, then the one above.
        var texts = new List<string>();
        foreach (var number in new[] { rowNumber, rowNumber + 1, rowNumber - 1 })
        {
            if (sheet.Row(number) is not { } row || number < profile.FirstDataRow)
            {
                continue;
            }

            var parts = profile.NotesColumns.Where(c => c > 0).Select(c => row.Cell(c).Text.Trim()).Where(t => t.Length > 0);
            texts.Add(string.Join(" ", parts));
        }

        DateOnly? date = null;
        var meters = new List<NoteMeterEntry>();
        foreach (var text in texts)
        {
            var note = ReplacementNoteParser.Parse(text);
            date ??= note.Date;
            if (meters.Count == 0)
            {
                meters.AddRange(note.Meters);
            }
        }

        return new ReplacementNote(date, meters);
    }

    private static NoteMeterEntry? PickEntry(ReplacementNote note, Meter oldMeter, List<Position> positions, Position position, int rowNumber)
    {
        var named = note.Meters.FirstOrDefault(m => m.Resource == oldMeter.Resource);
        if (named is not null)
        {
            return named;
        }

        var unnamed = note.Meters.Where(m => m.Resource is null).ToList();
        if (unnamed.Count == 0)
        {
            return null;
        }

        // Unnamed entries follow the order of the columns of the positions that are replaced in this row.
        var replacedHere = positions
            .Where(p => p.Candidates.Any(c => c.Row.RowNumber == rowNumber))
            .OrderBy(p => p.Mapping.ReadingColumn)
            .ToList();
        var index = Math.Max(0, replacedHere.IndexOf(position));
        return index < unnamed.Count ? unnamed[index] : null;
    }

    // Final values that the sheet does not give: the consumption of the row that the new meters do not explain belongs to the old meters.
    private static void EstimateFinals(ImportProfile profile, SheetData sheet, List<EstimateDraft> drafts, List<ReadingCandidate> candidates)
    {
        foreach (var group in drafts.GroupBy(d => d.Candidate.Row.RowNumber))
        {
            var items = group.ToList();
            decimal? consumption = null;
            if (profile.QuantityColumn > 0 && sheet.Row(group.Key)?.Cell(profile.QuantityColumn) is { IsEmpty: false } cell
                && ImportCellParser.TryParseDecimal(cell, out var parsed))
            {
                consumption = parsed;
            }

            if (consumption is null)
            {
                continue;
            }

            var rowCandidates = candidates.Where(c => c.Row.RowNumber == group.Key && !c.Skipped).ToList();
            var others = rowCandidates
                .Where(c => c.Line is null && c.PreviousValue is not null && c.Value >= c.PreviousValue)
                .Sum(c => c.Value - c.PreviousValue!.Value);

            // What the new meters counted since their installation, and what the old ones counted where their final value is known.
            var explained = 0m;
            foreach (var candidate in rowCandidates.Where(c => c.Line is not null))
            {
                var line = candidate.Line!;
                explained += line.InitialIsRowReading ? 0m : Math.Max(0, candidate.Value - line.InitialReading.Value);
                if (items.All(i => i.Candidate != candidate))
                {
                    explained += Math.Max(0, line.FinalReading.Value - (candidate.PreviousValue ?? line.FinalReading.Value));
                }
            }

            var remaining = Math.Max(0, consumption.Value - others - explained);
            var weights = items.Select(i => i.Candidate.PreviousStep).ToList();
            var weightSum = weights.Sum();
            for (var i = 0; i < items.Count; i++)
            {
                var share = weightSum > 0 ? weights[i] / weightSum : 1m / items.Count;
                items[i].Line.FinalReading.Value = items[i].PreviousValue + Math.Round(remaining * share, 3, MidpointRounding.AwayFromZero);
            }
        }
    }

    private static void AssignAndValidate(List<Position> positions, List<PlannedReplacement> replacements)
    {
        foreach (var position in positions)
        {
            var perRegister = new Dictionary<Guid, List<ReadingCandidate>>();
            foreach (var candidate in position.Candidates.Where(c => !c.Row.HasErrors && !c.Skipped).OrderBy(c => c.Date).ThenBy(c => c.Row.RowNumber))
            {
                if (candidate.Line is { InitialIsRowReading: true } owned)
                {
                    // The reading of the installation day is the initial reading of the new meter.
                    candidate.Accepted = true;
                    candidate.Row.Readings.Add(new PlannedReading(owned.NewRegisterId, owned.InitialReading));
                    continue;
                }

                var segment = SegmentAt(position, candidate.Date);
                if (segment is null)
                {
                    Error(candidate.Row, ImportIssueCodes.ReadingOutsideInstallation, candidate.Column, candidate.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
                    continue;
                }

                if (!perRegister.TryGetValue(segment.Register.Id, out var list))
                {
                    perRegister[segment.Register.Id] = list = [];
                }

                list.Add(candidate);
                candidate.Segment = segment;
            }

            foreach (var (registerId, list) in perRegister)
            {
                ValidateRegister(position, list, replacements);
            }
        }

        // Readings are attached after all checks so that a row skipped by a later check keeps none of them.
        foreach (var candidate in positions.SelectMany(p => p.Candidates).Where(c => c.Accepted && c.Line is not { InitialIsRowReading: true } && !c.Row.HasErrors))
        {
            candidate.Row.Readings.Add(new PlannedReading(
                candidate.Segment!.Register.Id,
                new MeterReading
                {
                    RegisterId = candidate.Segment.Register.Id,
                    Date = candidate.Date,
                    Value = candidate.Value,
                    Kind = ReadingKind.Regular,
                    Note = candidate.NoteToAttach,
                }));
        }
    }

    private static void ValidateRegister(Position position, List<ReadingCandidate> list, List<PlannedReplacement> replacements)
    {
        var segment = list[0].Segment!;
        var meter = segment.Meter;
        var register = segment.Register;
        var existing = register.Readings.Where(r => !r.IsDeleted).OrderBy(r => r.Date).ThenBy(r => r.CreatedAt).ToList();
        var accepted = new List<(DateOnly Date, decimal Value)>(existing.Select(r => (r.Date, r.Value)));
        foreach (var replacement in replacements)
        {
            foreach (var line in replacement.Registers)
            {
                if (line.OldRegisterId == register.Id)
                {
                    accepted.Add((line.FinalReading.Date, line.FinalReading.Value));
                }
                else if (line.NewRegisterId == register.Id)
                {
                    accepted.Add((line.InitialReading.Date, line.InitialReading.Value));
                }
            }
        }

        foreach (var candidate in list)
        {
            if (candidate.Row.HasErrors)
            {
                continue;
            }

            var text = candidate.Value.ToString(CultureInfo.InvariantCulture);
            if (accepted.Any(a => a.Date == candidate.Date))
            {
                Warning(candidate.Row, ImportIssueCodes.ReadingExists, candidate.Column, candidate.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
                continue;
            }

            var previous = accepted.Where(a => a.Date < candidate.Date).OrderByDescending(a => a.Date).Cast<(DateOnly Date, decimal Value)?>().FirstOrDefault();
            if (previous is { } p && candidate.Value < p.Value)
            {
                var previousText = p.Value.ToString(CultureInfo.InvariantCulture);
                if (meter.Digits is null)
                {
                    Error(candidate.Row, ImportIssueCodes.ReadingLowerThanPrevious, candidate.Column, text, previousText);
                    continue;
                }

                Warning(candidate.Row, ImportIssueCodes.ReadingRollover, candidate.Column, text, previousText);
            }

            var next = existing.FirstOrDefault(r => r.Date > candidate.Date);
            if (meter.Digits is null && next is not null && candidate.Value > next.Value)
            {
                Error(candidate.Row, ImportIssueCodes.ReadingHigherThanNext, candidate.Column, text, next.Value.ToString(CultureInfo.InvariantCulture));
                continue;
            }

            accepted.Add((candidate.Date, candidate.Value));
            candidate.Accepted = true;
        }
    }
}
