namespace Pos.Api.Models;

public class CustomerPricing
{
    public Guid CustomerId { get; set; }
    public Guid ProductId { get; set; }
    public decimal CustomPrice { get; set; }

    public Customer Customer { get; set; } = null!;
    public Product Product { get; set; } = null!;
}
