using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SistemiPos.Infrastructure.Persistence.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260909060000_StandardizeProductVariantDisplayNames")]
public sealed class StandardizeProductVariantDisplayNames : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            WITH VariantLabels AS
            (
                SELECT pvo.ProductId,
                    STRING_AGG(CONVERT(nvarchar(max), CONCAT(vt.Name, ' - ', vo.Value)), ' / ')
                        WITHIN GROUP (ORDER BY pvo.SortOrder) AS DisplayValue
                FROM ProductVariantOptions pvo
                INNER JOIN VariantOptions vo ON vo.Id = pvo.VariantOptionId
                INNER JOIN VariantTypes vt ON vt.Id = vo.VariantTypeId
                GROUP BY pvo.ProductId
            )
            UPDATE products
            SET VariantValue = labels.DisplayValue
            FROM Products products
            INNER JOIN VariantLabels labels ON labels.ProductId = products.Id
            WHERE products.ParentProductId IS NOT NULL;

            UPDATE saleItems
            SET ProductName = CONCAT(products.Name, ' ', products.VariantValue)
            FROM SaleItems saleItems
            INNER JOIN Products products ON products.Id = saleItems.ProductId
            WHERE products.ParentProductId IS NOT NULL
              AND products.VariantValue IS NOT NULL;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            WITH VariantValues AS
            (
                SELECT pvo.ProductId,
                    STRING_AGG(CONVERT(nvarchar(max), vo.Value), ' / ')
                        WITHIN GROUP (ORDER BY pvo.SortOrder) AS DisplayValue
                FROM ProductVariantOptions pvo
                INNER JOIN VariantOptions vo ON vo.Id = pvo.VariantOptionId
                GROUP BY pvo.ProductId
            )
            UPDATE products
            SET VariantValue = variantValues.DisplayValue
            FROM Products products
            INNER JOIN VariantValues variantValues ON variantValues.ProductId = products.Id
            WHERE products.ParentProductId IS NOT NULL;

            UPDATE saleItems
            SET ProductName = CONCAT(products.Name, ' ', products.VariantValue)
            FROM SaleItems saleItems
            INNER JOIN Products products ON products.Id = saleItems.ProductId
            WHERE products.ParentProductId IS NOT NULL
              AND products.VariantValue IS NOT NULL;
            """);
    }
}
