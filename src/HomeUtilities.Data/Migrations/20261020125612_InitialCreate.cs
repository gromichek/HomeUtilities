using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomeUtilities.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Categories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    SystemKey = table.Column<int>(type: "INTEGER", nullable: true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Color = table.Column<string>(type: "TEXT", maxLength: 7, nullable: false),
                    Icon = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    IsArchived = table.Column<bool>(type: "INTEGER", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Categories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Properties",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Kind = table.Column<int>(type: "INTEGER", nullable: false),
                    Address = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    IsArchived = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Properties", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Suppliers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    RegistrationNumber = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    Phone = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    Email = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    Links = table.Column<string>(type: "TEXT", nullable: false),
                    Pay_BankName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Pay_Bic = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    Pay_Iban = table.Column<string>(type: "TEXT", maxLength: 34, nullable: true),
                    Pay_Present = table.Column<int>(type: "INTEGER", nullable: true),
                    Pay_PurposeTemplate = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    Pay_RecipientName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Pay_RecipientRegistrationNumber = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Suppliers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "VatRates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Code = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VatRates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Meters",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    PropertyId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Resource = table.Column<int>(type: "INTEGER", nullable: false),
                    Unit = table.Column<int>(type: "INTEGER", nullable: false),
                    SerialNumber = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    Location = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Digits = table.Column<int>(type: "INTEGER", nullable: true),
                    InstalledOn = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    RemovedOn = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    ReplacedMeterId = table.Column<Guid>(type: "TEXT", nullable: true),
                    VerifiedOn = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    VerificationIntervalMonths = table.Column<int>(type: "INTEGER", nullable: true),
                    NextVerificationDue = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Meters", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Meters_Meters_ReplacedMeterId",
                        column: x => x.ReplacedMeterId,
                        principalTable: "Meters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Meters_Properties_PropertyId",
                        column: x => x.PropertyId,
                        principalTable: "Properties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PropertyParameters",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    PropertyId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Kind = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    ValidFrom = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Value = table.Column<decimal>(type: "TEXT", nullable: false),
                    Note = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PropertyParameters", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PropertyParameters_Properties_PropertyId",
                        column: x => x.PropertyId,
                        principalTable: "Properties",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Contracts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    PropertyId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SupplierId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Service = table.Column<int>(type: "INTEGER", nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    CategoryId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CustomerNumber = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    ContractNumber = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    PortalLogin = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    ValidFrom = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    ValidTo = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    Currency = table.Column<int>(type: "INTEGER", nullable: false),
                    BillingMode = table.Column<int>(type: "INTEGER", nullable: false),
                    ReadingDayFrom = table.Column<int>(type: "INTEGER", nullable: true),
                    ReadingDayTo = table.Column<int>(type: "INTEGER", nullable: true),
                    BillDay = table.Column<int>(type: "INTEGER", nullable: true),
                    PaymentDueDay = table.Column<int>(type: "INTEGER", nullable: true),
                    BillingTiming = table.Column<int>(type: "INTEGER", nullable: false),
                    CustomFields = table.Column<string>(type: "TEXT", nullable: false),
                    Links = table.Column<string>(type: "TEXT", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    IsArchived = table.Column<bool>(type: "INTEGER", nullable: false),
                    Pay_BankName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Pay_Bic = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    Pay_Iban = table.Column<string>(type: "TEXT", maxLength: 34, nullable: true),
                    Pay_Present = table.Column<int>(type: "INTEGER", nullable: true),
                    Pay_PurposeTemplate = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    Pay_RecipientName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Pay_RecipientRegistrationNumber = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Contracts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Contracts_Categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "Categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Contracts_Properties_PropertyId",
                        column: x => x.PropertyId,
                        principalTable: "Properties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Contracts_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "VatRateVersions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    VatRateId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ValidFrom = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Percent = table.Column<decimal>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VatRateVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VatRateVersions_VatRates_VatRateId",
                        column: x => x.VatRateId,
                        principalTable: "VatRates",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "MeterRegisters",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    MeterId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Kind = table.Column<int>(type: "INTEGER", nullable: false),
                    Direction = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MeterRegisters", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MeterRegisters_Meters_MeterId",
                        column: x => x.MeterId,
                        principalTable: "Meters",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Bills",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    PropertyId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CategoryId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ContractId = table.Column<Guid>(type: "TEXT", nullable: true),
                    SupplierId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Period = table.Column<int>(type: "INTEGER", nullable: false),
                    ServiceFrom = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    ServiceTo = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    Kind = table.Column<int>(type: "INTEGER", nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Number = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    IssuedOn = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    DueOn = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    Net = table.Column<decimal>(type: "TEXT", nullable: true),
                    VatPercent = table.Column<decimal>(type: "TEXT", nullable: true),
                    Vat = table.Column<decimal>(type: "TEXT", nullable: true),
                    Gross = table.Column<decimal>(type: "TEXT", nullable: false),
                    Currency = table.Column<int>(type: "INTEGER", nullable: false),
                    CustomFields = table.Column<string>(type: "TEXT", nullable: false),
                    ConsumptionOnBill = table.Column<decimal>(type: "TEXT", nullable: true),
                    CalculatedSnapshot = table.Column<string>(type: "TEXT", nullable: true),
                    TemplateId = table.Column<Guid>(type: "TEXT", nullable: true),
                    AttachmentIds = table.Column<string>(type: "TEXT", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Bills", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Bills_Categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "Categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Bills_Contracts_ContractId",
                        column: x => x.ContractId,
                        principalTable: "Contracts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Bills_Properties_PropertyId",
                        column: x => x.PropertyId,
                        principalTable: "Properties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Bills_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ContractMeters",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ContractId = table.Column<Guid>(type: "TEXT", nullable: false),
                    MeterId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ValidFrom = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    ValidTo = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContractMeters", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContractMeters_Contracts_ContractId",
                        column: x => x.ContractId,
                        principalTable: "Contracts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ContractMeters_Meters_MeterId",
                        column: x => x.MeterId,
                        principalTable: "Meters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Payments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ContractId = table.Column<Guid>(type: "TEXT", nullable: true),
                    PropertyId = table.Column<Guid>(type: "TEXT", nullable: true),
                    PaidOn = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Amount = table.Column<decimal>(type: "TEXT", nullable: false),
                    Currency = table.Column<int>(type: "INTEGER", nullable: false),
                    Method = table.Column<int>(type: "INTEGER", nullable: false),
                    Reference = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Payments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Payments_Contracts_ContractId",
                        column: x => x.ContractId,
                        principalTable: "Contracts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Payments_Properties_PropertyId",
                        column: x => x.PropertyId,
                        principalTable: "Properties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TariffComponents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ContractId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    ChargeKind = table.Column<int>(type: "INTEGER", nullable: false),
                    CategoryId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Basis = table.Column<int>(type: "INTEGER", nullable: false),
                    RegisterKinds = table.Column<string>(type: "TEXT", nullable: false),
                    MeterIds = table.Column<string>(type: "TEXT", nullable: false),
                    QuantityParameter = table.Column<int>(type: "INTEGER", nullable: true),
                    QuantityParameterName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    VatCode = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    VatPercentOverride = table.Column<decimal>(type: "TEXT", nullable: true),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TariffComponents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TariffComponents_Categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "Categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TariffComponents_Contracts_ContractId",
                        column: x => x.ContractId,
                        principalTable: "Contracts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MeterReadings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    RegisterId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Date = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Value = table.Column<decimal>(type: "TEXT", nullable: false),
                    Kind = table.Column<int>(type: "INTEGER", nullable: false),
                    SubmittedOn = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    PhotoAttachmentId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Note = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MeterReadings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MeterReadings_MeterRegisters_RegisterId",
                        column: x => x.RegisterId,
                        principalTable: "MeterRegisters",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "BillLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    BillId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Quantity = table.Column<decimal>(type: "TEXT", nullable: true),
                    Unit = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    UnitPrice = table.Column<decimal>(type: "TEXT", nullable: true),
                    Net = table.Column<decimal>(type: "TEXT", nullable: true),
                    VatPercent = table.Column<decimal>(type: "TEXT", nullable: true),
                    Vat = table.Column<decimal>(type: "TEXT", nullable: true),
                    Gross = table.Column<decimal>(type: "TEXT", nullable: false),
                    CategoryId = table.Column<Guid>(type: "TEXT", nullable: true),
                    CustomFields = table.Column<string>(type: "TEXT", nullable: false),
                    Note = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BillLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BillLines_Bills_BillId",
                        column: x => x.BillId,
                        principalTable: "Bills",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_BillLines_Categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "Categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PaymentAllocations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    PaymentId = table.Column<Guid>(type: "TEXT", nullable: false),
                    BillId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Amount = table.Column<decimal>(type: "TEXT", nullable: false),
                    Note = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentAllocations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PaymentAllocations_Bills_BillId",
                        column: x => x.BillId,
                        principalTable: "Bills",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PaymentAllocations_Payments_PaymentId",
                        column: x => x.PaymentId,
                        principalTable: "Payments",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "TariffRates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ComponentId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ValidFrom = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Price = table.Column<decimal>(type: "TEXT", nullable: false),
                    PriceIncludesVat = table.Column<bool>(type: "INTEGER", nullable: false),
                    Currency = table.Column<int>(type: "INTEGER", nullable: false),
                    Tiers = table.Column<string>(type: "TEXT", nullable: false),
                    TierPeriod = table.Column<int>(type: "INTEGER", nullable: true),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<long>(type: "INTEGER", nullable: false),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TariffRates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TariffRates_TariffComponents_ComponentId",
                        column: x => x.ComponentId,
                        principalTable: "TariffComponents",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_BillLines_BillId",
                table: "BillLines",
                column: "BillId");

            migrationBuilder.CreateIndex(
                name: "IX_BillLines_CategoryId",
                table: "BillLines",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_BillLines_UpdatedAt",
                table: "BillLines",
                column: "UpdatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Bills_CategoryId",
                table: "Bills",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Bills_ContractId_Period",
                table: "Bills",
                columns: new[] { "ContractId", "Period" });

            migrationBuilder.CreateIndex(
                name: "IX_Bills_PropertyId_Period",
                table: "Bills",
                columns: new[] { "PropertyId", "Period" });

            migrationBuilder.CreateIndex(
                name: "IX_Bills_SupplierId",
                table: "Bills",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_Bills_UpdatedAt",
                table: "Bills",
                column: "UpdatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Categories_SystemKey",
                table: "Categories",
                column: "SystemKey",
                unique: true,
                filter: "\"SystemKey\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Categories_UpdatedAt",
                table: "Categories",
                column: "UpdatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ContractMeters_ContractId",
                table: "ContractMeters",
                column: "ContractId");

            migrationBuilder.CreateIndex(
                name: "IX_ContractMeters_MeterId",
                table: "ContractMeters",
                column: "MeterId");

            migrationBuilder.CreateIndex(
                name: "IX_ContractMeters_UpdatedAt",
                table: "ContractMeters",
                column: "UpdatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Contracts_CategoryId",
                table: "Contracts",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Contracts_PropertyId",
                table: "Contracts",
                column: "PropertyId");

            migrationBuilder.CreateIndex(
                name: "IX_Contracts_SupplierId",
                table: "Contracts",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_Contracts_UpdatedAt",
                table: "Contracts",
                column: "UpdatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_MeterReadings_RegisterId_Date",
                table: "MeterReadings",
                columns: new[] { "RegisterId", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_MeterReadings_UpdatedAt",
                table: "MeterReadings",
                column: "UpdatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_MeterRegisters_MeterId",
                table: "MeterRegisters",
                column: "MeterId");

            migrationBuilder.CreateIndex(
                name: "IX_MeterRegisters_UpdatedAt",
                table: "MeterRegisters",
                column: "UpdatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Meters_PropertyId",
                table: "Meters",
                column: "PropertyId");

            migrationBuilder.CreateIndex(
                name: "IX_Meters_ReplacedMeterId",
                table: "Meters",
                column: "ReplacedMeterId");

            migrationBuilder.CreateIndex(
                name: "IX_Meters_UpdatedAt",
                table: "Meters",
                column: "UpdatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentAllocations_BillId",
                table: "PaymentAllocations",
                column: "BillId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentAllocations_PaymentId",
                table: "PaymentAllocations",
                column: "PaymentId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentAllocations_UpdatedAt",
                table: "PaymentAllocations",
                column: "UpdatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_ContractId",
                table: "Payments",
                column: "ContractId");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_PropertyId",
                table: "Payments",
                column: "PropertyId");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_UpdatedAt",
                table: "Payments",
                column: "UpdatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Properties_UpdatedAt",
                table: "Properties",
                column: "UpdatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_PropertyParameters_PropertyId",
                table: "PropertyParameters",
                column: "PropertyId");

            migrationBuilder.CreateIndex(
                name: "IX_PropertyParameters_UpdatedAt",
                table: "PropertyParameters",
                column: "UpdatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Suppliers_UpdatedAt",
                table: "Suppliers",
                column: "UpdatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_TariffComponents_CategoryId",
                table: "TariffComponents",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_TariffComponents_ContractId",
                table: "TariffComponents",
                column: "ContractId");

            migrationBuilder.CreateIndex(
                name: "IX_TariffComponents_UpdatedAt",
                table: "TariffComponents",
                column: "UpdatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_TariffRates_ComponentId_ValidFrom",
                table: "TariffRates",
                columns: new[] { "ComponentId", "ValidFrom" });

            migrationBuilder.CreateIndex(
                name: "IX_TariffRates_UpdatedAt",
                table: "TariffRates",
                column: "UpdatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_VatRates_Code",
                table: "VatRates",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VatRates_UpdatedAt",
                table: "VatRates",
                column: "UpdatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_VatRateVersions_UpdatedAt",
                table: "VatRateVersions",
                column: "UpdatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_VatRateVersions_VatRateId",
                table: "VatRateVersions",
                column: "VatRateId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BillLines");

            migrationBuilder.DropTable(
                name: "ContractMeters");

            migrationBuilder.DropTable(
                name: "MeterReadings");

            migrationBuilder.DropTable(
                name: "PaymentAllocations");

            migrationBuilder.DropTable(
                name: "PropertyParameters");

            migrationBuilder.DropTable(
                name: "TariffRates");

            migrationBuilder.DropTable(
                name: "VatRateVersions");

            migrationBuilder.DropTable(
                name: "MeterRegisters");

            migrationBuilder.DropTable(
                name: "Bills");

            migrationBuilder.DropTable(
                name: "Payments");

            migrationBuilder.DropTable(
                name: "TariffComponents");

            migrationBuilder.DropTable(
                name: "VatRates");

            migrationBuilder.DropTable(
                name: "Meters");

            migrationBuilder.DropTable(
                name: "Contracts");

            migrationBuilder.DropTable(
                name: "Categories");

            migrationBuilder.DropTable(
                name: "Properties");

            migrationBuilder.DropTable(
                name: "Suppliers");
        }
    }
}
