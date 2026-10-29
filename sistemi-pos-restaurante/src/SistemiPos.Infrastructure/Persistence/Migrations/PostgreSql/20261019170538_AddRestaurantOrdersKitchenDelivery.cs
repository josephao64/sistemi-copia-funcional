using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SistemiPos.Infrastructure.Persistence.Migrations.PostgreSql
{
    /// <inheritdoc />
    public partial class AddRestaurantOrdersKitchenDelivery : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateSequence(
                name: "RestaurantOrderNumberSequence");

            migrationBuilder.AddColumn<decimal>(
                name: "ReservedQuantity",
                table: "InventoryStocks",
                type: "numeric(18,6)",
                precision: 18,
                scale: 6,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<Guid>(
                name: "OriginalMovementId",
                table: "InventoryMovements",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "RestaurantCommands",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OperationId = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ResultId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RestaurantCommands", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RestaurantSetups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    DefinitionJson = table.Column<string>(type: "text", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    ModifiedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RestaurantSetups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RestaurantSetups_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RestaurantTableTurns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    TableId = table.Column<Guid>(type: "uuid", nullable: false),
                    TableName = table.Column<string>(type: "text", nullable: false),
                    OpenedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ClosedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    ModifiedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RestaurantTableTurns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RestaurantTableTurns_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RestaurantOrders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    WarehouseId = table.Column<Guid>(type: "uuid", nullable: false),
                    Number = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ChannelId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChannelName = table.Column<string>(type: "text", nullable: false),
                    ExternalReference = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    ServiceType = table.Column<int>(type: "integer", nullable: false),
                    TableTurnId = table.Column<Guid>(type: "uuid", nullable: true),
                    CustomerName = table.Column<string>(type: "text", nullable: false),
                    Phone = table.Column<string>(type: "text", nullable: false),
                    Address = table.Column<string>(type: "text", nullable: false),
                    DeliveryZoneName = table.Column<string>(type: "text", nullable: false),
                    DeliveryFee = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: false),
                    DraftJson = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    DriverId = table.Column<Guid>(type: "uuid", nullable: true),
                    DriverName = table.Column<string>(type: "text", nullable: true),
                    Route = table.Column<string>(type: "text", nullable: true),
                    AssignedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DepartedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeliveredAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    ModifiedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RestaurantOrders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RestaurantOrders_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RestaurantOrders_RestaurantTableTurns_TableTurnId",
                        column: x => x.TableTurnId,
                        principalTable: "RestaurantTableTurns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RestaurantOrders_Warehouses_WarehouseId",
                        column: x => x.WarehouseId,
                        principalTable: "Warehouses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RestaurantEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    LineId = table.Column<Guid>(type: "uuid", nullable: true),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    Action = table.Column<string>(type: "text", nullable: false),
                    Actor = table.Column<string>(type: "text", nullable: false),
                    AtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Detail = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RestaurantEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RestaurantEvents_RestaurantOrders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "RestaurantOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RestaurantOrderLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    SentRevision = table.Column<int>(type: "integer", nullable: false),
                    SnapshotJson = table.Column<string>(type: "text", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    ParentLineId = table.Column<Guid>(type: "uuid", nullable: true),
                    StationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    CancelledQuantity = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    WasteQuantity = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    PreparedQuantity = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    ReturnedQuantity = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    QueuedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ReadyAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    HandedOffAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RestaurantOrderLines", x => x.Id);
                    table.CheckConstraint("CK_RestaurantLine_Preparation", "\"PreparedQuantity\" >= 0 AND \"PreparedQuantity\" <= \"Quantity\" AND \"ReturnedQuantity\" >= 0 AND \"ReturnedQuantity\" + \"WasteQuantity\" <= \"PreparedQuantity\" AND \"ReturnedQuantity\" + \"WasteQuantity\" <= \"CancelledQuantity\"");
                    table.CheckConstraint("CK_RestaurantLine_Quantity", "\"Quantity\" > 0 AND \"CancelledQuantity\" >= 0 AND \"CancelledQuantity\" <= \"Quantity\" AND \"WasteQuantity\" >= 0 AND \"WasteQuantity\" <= \"CancelledQuantity\"");
                    table.ForeignKey(
                        name: "FK_RestaurantOrderLines_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RestaurantOrderLines_RestaurantOrderLines_ParentLineId",
                        column: x => x.ParentLineId,
                        principalTable: "RestaurantOrderLines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RestaurantOrderLines_RestaurantOrders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "RestaurantOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OrderReservations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    LineId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    WarehouseId = table.Column<Guid>(type: "uuid", nullable: false),
                    LotId = table.Column<Guid>(type: "uuid", nullable: true),
                    Quantity = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    UnitCost = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    RestoredQuantity = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    MovementId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderReservations", x => x.Id);
                    table.CheckConstraint("CK_OrderReservation_Quantity", "\"Quantity\" >= 0");
                    table.CheckConstraint("CK_OrderReservation_Restored", "\"RestoredQuantity\" >= 0 AND \"RestoredQuantity\" <= \"Quantity\"");
                    table.ForeignKey(
                        name: "FK_OrderReservations_InventoryExpiryLots_LotId",
                        column: x => x.LotId,
                        principalTable: "InventoryExpiryLots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OrderReservations_InventoryMovements_MovementId",
                        column: x => x.MovementId,
                        principalTable: "InventoryMovements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OrderReservations_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OrderReservations_RestaurantOrderLines_LineId",
                        column: x => x.LineId,
                        principalTable: "RestaurantOrderLines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OrderReservations_RestaurantOrders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "RestaurantOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OrderReservations_Warehouses_WarehouseId",
                        column: x => x.WarehouseId,
                        principalTable: "Warehouses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OrderWaste",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    LineId = table.Column<Guid>(type: "uuid", nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    Cost = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    Reason = table.Column<string>(type: "text", nullable: false),
                    Actor = table.Column<string>(type: "text", nullable: false),
                    AtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ConsumptionJson = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderWaste", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrderWaste_RestaurantOrderLines_LineId",
                        column: x => x.LineId,
                        principalTable: "RestaurantOrderLines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OrderWaste_RestaurantOrders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "RestaurantOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_InventoryStock_Reservations",
                table: "InventoryStocks",
                sql: "\"ReservedQuantity\" >= 0 AND \"CurrentStock\" >= \"ReservedQuantity\"");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryMovements_OriginalMovementId",
                table: "InventoryMovements",
                column: "OriginalMovementId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderReservations_LineId",
                table: "OrderReservations",
                column: "LineId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderReservations_LotId_Status",
                table: "OrderReservations",
                columns: new[] { "LotId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_OrderReservations_MovementId",
                table: "OrderReservations",
                column: "MovementId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderReservations_OrderId",
                table: "OrderReservations",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderReservations_ProductId_WarehouseId_Status",
                table: "OrderReservations",
                columns: new[] { "ProductId", "WarehouseId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_OrderReservations_WarehouseId",
                table: "OrderReservations",
                column: "WarehouseId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderWaste_LineId",
                table: "OrderWaste",
                column: "LineId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderWaste_OrderId",
                table: "OrderWaste",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_RestaurantCommands_OperationId",
                table: "RestaurantCommands",
                column: "OperationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RestaurantEvents_OrderId_Revision",
                table: "RestaurantEvents",
                columns: new[] { "OrderId", "Revision" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RestaurantOrderLines_OrderId",
                table: "RestaurantOrderLines",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_RestaurantOrderLines_ParentLineId",
                table: "RestaurantOrderLines",
                column: "ParentLineId");

            migrationBuilder.CreateIndex(
                name: "IX_RestaurantOrderLines_ProductId",
                table: "RestaurantOrderLines",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_RestaurantOrderLines_StationId_Status",
                table: "RestaurantOrderLines",
                columns: new[] { "StationId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_RestaurantOrders_BranchId_ChannelId_ExternalReference",
                table: "RestaurantOrders",
                columns: new[] { "BranchId", "ChannelId", "ExternalReference" },
                unique: true,
                filter: "\"ExternalReference\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_RestaurantOrders_BranchId_Status",
                table: "RestaurantOrders",
                columns: new[] { "BranchId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_RestaurantOrders_Number",
                table: "RestaurantOrders",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RestaurantOrders_TableTurnId",
                table: "RestaurantOrders",
                column: "TableTurnId");

            migrationBuilder.CreateIndex(
                name: "IX_RestaurantOrders_WarehouseId",
                table: "RestaurantOrders",
                column: "WarehouseId");

            migrationBuilder.CreateIndex(
                name: "IX_RestaurantSetups_BranchId",
                table: "RestaurantSetups",
                column: "BranchId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RestaurantTableTurns_BranchId_TableId",
                table: "RestaurantTableTurns",
                columns: new[] { "BranchId", "TableId" },
                unique: true,
                filter: "\"ClosedAtUtc\" IS NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_InventoryMovements_InventoryMovements_OriginalMovementId",
                table: "InventoryMovements",
                column: "OriginalMovementId",
                principalTable: "InventoryMovements",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_InventoryMovements_InventoryMovements_OriginalMovementId",
                table: "InventoryMovements");

            migrationBuilder.DropTable(
                name: "OrderReservations");

            migrationBuilder.DropTable(
                name: "OrderWaste");

            migrationBuilder.DropTable(
                name: "RestaurantCommands");

            migrationBuilder.DropTable(
                name: "RestaurantEvents");

            migrationBuilder.DropTable(
                name: "RestaurantSetups");

            migrationBuilder.DropTable(
                name: "RestaurantOrderLines");

            migrationBuilder.DropTable(
                name: "RestaurantOrders");

            migrationBuilder.DropTable(
                name: "RestaurantTableTurns");

            migrationBuilder.DropCheckConstraint(
                name: "CK_InventoryStock_Reservations",
                table: "InventoryStocks");

            migrationBuilder.DropIndex(
                name: "IX_InventoryMovements_OriginalMovementId",
                table: "InventoryMovements");

            migrationBuilder.DropColumn(
                name: "ReservedQuantity",
                table: "InventoryStocks");

            migrationBuilder.DropColumn(
                name: "OriginalMovementId",
                table: "InventoryMovements");

            migrationBuilder.DropSequence(
                name: "RestaurantOrderNumberSequence");
        }
    }
}
