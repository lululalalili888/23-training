using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace OrderHub.Web.ViewModels;

public class LowStockListViewModel
{
    [Range(1, int.MaxValue, ErrorMessage = "門檻必須大於 0")]
    public int Threshold { get; set; } = 10;

    [BindNever]
    public IReadOnlyList<LowStockRowViewModel> Products { get; set; } = Array.Empty<LowStockRowViewModel>();
}

public class LowStockRowViewModel
{
    public string Sku { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int StockQuantity { get; set; }
    public int QuantitySoldLast30Days { get; set; }
}
