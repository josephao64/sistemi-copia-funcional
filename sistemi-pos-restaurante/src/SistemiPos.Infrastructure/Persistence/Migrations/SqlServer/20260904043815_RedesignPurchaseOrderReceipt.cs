using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SistemiPos.Infrastructure.Persistence.Migrations;

public partial class RedesignPurchaseOrderReceipt : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("IF COL_LENGTH('Products', 'SupplierInvoiceName') IS NULL ALTER TABLE Products ADD SupplierInvoiceName nvarchar(300) NULL;");
        migrationBuilder.AddColumn<string>("BusinessEmail", "SystemSettings", "nvarchar(200)", maxLength: 200, nullable: true);
        migrationBuilder.AddColumn<string>("InvoiceNumber", "Purchases", "nvarchar(100)", maxLength: 100, nullable: false, defaultValue: "");
        migrationBuilder.AddColumn<string>("Observations", "Purchases", "nvarchar(1000)", maxLength: 1000, nullable: true);
        migrationBuilder.AddColumn<Guid>("PurchaseOrderId", "Purchases", "uniqueidentifier", nullable: true);
        migrationBuilder.Sql("UPDATE Purchases SET InvoiceNumber = Number WHERE InvoiceNumber = '';");
        migrationBuilder.AddColumn<string>("Observations", "PurchaseOrders", "nvarchar(1000)", maxLength: 1000, nullable: true);
        migrationBuilder.AddColumn<DateTimeOffset>("ReceivedAtUtc", "PurchaseOrders", "datetimeoffset", nullable: true);
        migrationBuilder.AddColumn<DateTimeOffset>("SubmittedAtUtc", "PurchaseOrders", "datetimeoffset", nullable: true);
        migrationBuilder.AddColumn<decimal>("EstimatedUnitCost", "PurchaseOrderItems", "decimal(18,2)", nullable: false, defaultValue: 0m);
        migrationBuilder.AddColumn<Guid>("PurchaseId", "InventoryMovements", "uniqueidentifier", nullable: true);
        migrationBuilder.AddColumn<Guid>("PurchaseOrderId", "InventoryMovements", "uniqueidentifier", nullable: true);

        migrationBuilder.CreateTable(
            name: "PurchaseReceipts",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Number = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                PurchaseOrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                PurchaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                SupplierId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                SupplierDocumentNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                SupplierDocumentDateUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                ReceivedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                Terms = table.Column<int>(type: "int", nullable: false),
                CreditDays = table.Column<int>(type: "int", nullable: true),
                DueDateUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                Observations = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                Total = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                ModifiedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                ModifiedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PurchaseReceipts", x => x.Id);
                table.ForeignKey("FK_PurchaseReceipts_PurchaseOrders_PurchaseOrderId", x => x.PurchaseOrderId, "PurchaseOrders", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_PurchaseReceipts_Purchases_PurchaseId", x => x.PurchaseId, "Purchases", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_PurchaseReceipts_Suppliers_SupplierId", x => x.SupplierId, "Suppliers", "Id", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "PurchaseReceiptItems",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                PurchaseReceiptId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                PurchaseOrderItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                RequestedQuantity = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                ReceivedQuantity = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                ActualUnitCost = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                IsAdditional = table.Column<bool>(type: "bit", nullable: false),
                Subtotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PurchaseReceiptItems", x => x.Id);
                table.ForeignKey("FK_PurchaseReceiptItems_Products_ProductId", x => x.ProductId, "Products", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_PurchaseReceiptItems_PurchaseOrderItems_PurchaseOrderItemId", x => x.PurchaseOrderItemId, "PurchaseOrderItems", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_PurchaseReceiptItems_PurchaseReceipts_PurchaseReceiptId", x => x.PurchaseReceiptId, "PurchaseReceipts", "Id", onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex("IX_InventoryMovements_PurchaseId", "InventoryMovements", "PurchaseId");
        migrationBuilder.CreateIndex("IX_InventoryMovements_PurchaseOrderId", "InventoryMovements", "PurchaseOrderId");
        migrationBuilder.CreateIndex("IX_Purchases_PurchaseOrderId", "Purchases", "PurchaseOrderId", unique: true, filter: "[PurchaseOrderId] IS NOT NULL");
        migrationBuilder.CreateIndex("IX_PurchaseReceipts_Number", "PurchaseReceipts", "Number", unique: true);
        migrationBuilder.CreateIndex("IX_PurchaseReceipts_PurchaseId", "PurchaseReceipts", "PurchaseId", unique: true);
        migrationBuilder.CreateIndex("IX_PurchaseReceipts_PurchaseOrderId", "PurchaseReceipts", "PurchaseOrderId", unique: true);
        migrationBuilder.CreateIndex("IX_PurchaseReceipts_SupplierId", "PurchaseReceipts", "SupplierId");
        migrationBuilder.CreateIndex("IX_PurchaseReceiptItems_ProductId", "PurchaseReceiptItems", "ProductId");
        migrationBuilder.CreateIndex("IX_PurchaseReceiptItems_PurchaseOrderItemId", "PurchaseReceiptItems", "PurchaseOrderItemId");
        migrationBuilder.CreateIndex("IX_PurchaseReceiptItems_PurchaseReceiptId_ProductId", "PurchaseReceiptItems", new[] { "PurchaseReceiptId", "ProductId" }, unique: true);
        migrationBuilder.AddForeignKey("FK_Purchases_PurchaseOrders_PurchaseOrderId", "Purchases", "PurchaseOrderId", "PurchaseOrders", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
        migrationBuilder.AddForeignKey("FK_InventoryMovements_PurchaseOrders_PurchaseOrderId", "InventoryMovements", "PurchaseOrderId", "PurchaseOrders", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
        migrationBuilder.AddForeignKey("FK_InventoryMovements_Purchases_PurchaseId", "InventoryMovements", "PurchaseId", "Purchases", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
        migrationBuilder.Sql("UPDATE PurchaseOrders SET SubmittedAtUtc = RequestedAtUtc WHERE Status IN (1, 2, 3) AND SubmittedAtUtc IS NULL; UPDATE PurchaseOrders SET Status = 1 WHERE Status = 2;");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("PurchaseReceiptItems");
        migrationBuilder.DropTable("PurchaseReceipts");
        migrationBuilder.DropForeignKey("FK_InventoryMovements_PurchaseOrders_PurchaseOrderId", "InventoryMovements");
        migrationBuilder.DropForeignKey("FK_InventoryMovements_Purchases_PurchaseId", "InventoryMovements");
        migrationBuilder.DropForeignKey("FK_Purchases_PurchaseOrders_PurchaseOrderId", "Purchases");
        migrationBuilder.DropIndex("IX_InventoryMovements_PurchaseId", "InventoryMovements");
        migrationBuilder.DropIndex("IX_InventoryMovements_PurchaseOrderId", "InventoryMovements");
        migrationBuilder.DropIndex("IX_Purchases_PurchaseOrderId", "Purchases");
        migrationBuilder.DropColumn("BusinessEmail", "SystemSettings");
        migrationBuilder.DropColumn("InvoiceNumber", "Purchases");
        migrationBuilder.DropColumn("Observations", "Purchases");
        migrationBuilder.DropColumn("PurchaseOrderId", "Purchases");
        migrationBuilder.DropColumn("Observations", "PurchaseOrders");
        migrationBuilder.DropColumn("ReceivedAtUtc", "PurchaseOrders");
        migrationBuilder.DropColumn("SubmittedAtUtc", "PurchaseOrders");
        migrationBuilder.DropColumn("EstimatedUnitCost", "PurchaseOrderItems");
        migrationBuilder.DropColumn("PurchaseId", "InventoryMovements");
        migrationBuilder.DropColumn("PurchaseOrderId", "InventoryMovements");
    }
}
