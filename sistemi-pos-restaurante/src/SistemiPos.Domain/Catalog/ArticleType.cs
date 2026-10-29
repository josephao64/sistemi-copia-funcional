namespace SistemiPos.Domain.Catalog;

public enum ArticleType
{
    Insumo = 1,
    Preparado = 2,
    Reventa = 3,
    Subreceta = 4,
    Empaque = 5,
    Ingrediente = 6,
    ProductoDeVenta = 7,
    Bebida = 8
}

public static class ArticleTypes
{
    public static IReadOnlyList<ArticleType> Current { get; } =
        [ArticleType.Insumo, ArticleType.Preparado, ArticleType.Reventa, ArticleType.Subreceta, ArticleType.Empaque];

    public static string Label(this ArticleType type) => type == ArticleType.ProductoDeVenta
        ? "Producto de venta" : type.ToString();
}
