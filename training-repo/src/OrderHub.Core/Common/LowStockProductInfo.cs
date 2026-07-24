using OrderHub.Core.Domain;

namespace OrderHub.Core.Common;

public record LowStockProductInfo(Product Product, int QuantitySoldLast30Days);
