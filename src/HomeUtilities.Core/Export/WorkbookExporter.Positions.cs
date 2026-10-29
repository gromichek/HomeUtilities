using HomeUtilities.Core.Common;
using HomeUtilities.Core.Consumption;
using HomeUtilities.Core.Domain;
using HomeUtilities.Core.Reports;

namespace HomeUtilities.Core.Export;

public static partial class WorkbookExporter
{
    private sealed record ReadingRef(MeterReading Reading, int MeterIndex);

    // A replacement inside the chain of one position, seen from the register: the old meter's final reading and the new meter's initial one.
    private sealed record ReplacementRef(DateOnly Date, Meter OldMeter, Meter NewMeter, decimal? OldFinal, MeterReading? NewInitial);

    // A position is a register followed through the replacements of its meter: the meters that replaced one another, seen as one meter over time.
    private sealed class Position(Guid key, MeterRegister head, IReadOnlyList<Meter> chain, int readingColumn, int dateColumn)
    {
        public Guid Key { get; } = key;

        public MeterRegister Head { get; } = head;

        public IReadOnlyList<Meter> Chain { get; } = chain;

        public int ReadingColumn { get; } = readingColumn;

        public int DateColumn { get; } = dateColumn;

        /// <summary>The readings that become cells: everything except the final readings of replaced meters and the initial readings of replacement meters.</summary>
        public List<ReadingRef> Series { get; } = [];

        public List<ReplacementRef> Replacements { get; } = [];
    }

    private static MeterRegister? RegisterOf(Meter meter, MeterRegister like) =>
        meter.Registers.FirstOrDefault(r => !r.IsDeleted && r.Kind == like.Kind && r.Direction == like.Direction && r.Name == like.Name)
        ?? meter.Registers.FirstOrDefault(r => !r.IsDeleted && r.Kind == like.Kind && r.Direction == like.Direction);

    private static Position? BuildPosition(IReadOnlyList<Meter> allMeters, MeterRegister register, Meter meter, int readingColumn, int dateColumn)
    {
        var chain = MeterChain.Build(allMeters, meter.Id).Meters;
        var head = RegisterOf(chain[0], register);
        if (head is null)
        {
            return null;
        }

        var position = new Position(head.Id, head, chain, readingColumn, dateColumn);
        for (var index = 0; index < chain.Count; index++)
        {
            var own = RegisterOf(chain[index], head);
            if (own is null)
            {
                continue;
            }

            var next = index + 1 < chain.Count ? chain[index + 1] : null;
            var previous = index > 0 ? chain[index - 1] : null;
            foreach (var reading in own.Readings.Where(r => !r.IsDeleted).OrderBy(r => r.Date).ThenBy(r => r.CreatedAt))
            {
                if (next is not null && IsFinalReading(reading, next))
                {
                    continue;
                }

                if (previous is not null && reading.Kind == ReadingKind.Initial && reading.Date == chain[index].InstalledOn)
                {
                    continue;
                }

                position.Series.Add(new ReadingRef(reading, index));
            }
        }

        for (var index = 1; index < chain.Count; index++)
        {
            var oldRegister = RegisterOf(chain[index - 1], head);
            var newRegister = RegisterOf(chain[index], head);
            var date = chain[index].InstalledOn;
            var final = oldRegister?.Readings
                .Where(r => !r.IsDeleted && IsFinalReading(r, chain[index]))
                .OrderByDescending(r => r.Date).ThenByDescending(r => r.CreatedAt)
                .FirstOrDefault();
            var initial = newRegister?.Readings
                .Where(r => !r.IsDeleted && r.Kind == ReadingKind.Initial && r.Date == date)
                .OrderBy(r => r.CreatedAt)
                .FirstOrDefault();
            position.Replacements.Add(new ReplacementRef(date, chain[index - 1], chain[index], final?.Value, initial));
        }

        return position;
    }

    // The last reading of a replaced meter: marked Final, or an estimate dated with the replacement.
    private static bool IsFinalReading(MeterReading reading, Meter replacement) =>
        reading.Kind == ReadingKind.Final || (reading.Kind == ReadingKind.Estimated && reading.Date == replacement.InstalledOn);

    private static List<Position> PositionsOf(ReportData data, Contract contract, Import.ImportProfile? profile)
    {
        var positions = new List<Position>();
        var registers = data.Meters
            .SelectMany(m => m.Registers.Where(r => !r.IsDeleted).Select(r => (Meter: m, Register: r)))
            .ToDictionary(x => x.Register.Id);

        if (profile is not null)
        {
            foreach (var mapping in profile.Registers.Where(r => r.ReadingColumn > 0).OrderBy(r => r.ReadingColumn))
            {
                if (!registers.TryGetValue(mapping.RegisterId, out var entry))
                {
                    continue;
                }

                var position = BuildPosition(data.Meters, entry.Register, entry.Meter, mapping.ReadingColumn, mapping.DateColumn);
                if (position is not null && positions.All(p => p.Key != position.Key))
                {
                    positions.Add(position);
                }
            }

            return positions;
        }

        var linked = data.ContractMeters.Where(l => l.ContractId == contract.Id).Select(l => l.MeterId).Distinct();
        foreach (var meter in data.Meters.Where(m => linked.Contains(m.Id)).OrderBy(m => m.Resource).ThenBy(m => m.InstalledOn))
        {
            foreach (var register in meter.Registers.Where(r => !r.IsDeleted).OrderBy(r => r.Kind).ThenBy(r => r.Direction))
            {
                var position = BuildPosition(data.Meters, register, meter, 0, 0);
                if (position is not null && positions.All(p => p.Key != position.Key))
                {
                    positions.Add(position);
                }
            }
        }

        return positions;
    }
}
