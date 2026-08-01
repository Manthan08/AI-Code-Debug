using System.Globalization;

Console.WriteLine("Visual Studio Debug Bridge sample");

var orders = new[]
{
    new Order("A100", 125m),
    new Order("A101", 80m)
};

var customerId = args.Length == 0 ? "missing-customer" : args[0];
var customer = FindCustomer(customerId);
var invoiceTotal = CreateInvoice(customer, orders);

Console.WriteLine("Invoice total: " + invoiceTotal.ToString("C", CultureInfo.CurrentCulture));

static Customer? FindCustomer(string customerId)
{
    if (customerId == "premium")
    {
        return new Customer(customerId, "Premium customer", true);
    }

    return null;
}

static decimal CreateInvoice(Customer? customer, IReadOnlyCollection<Order> orders)
{
    var subtotal = orders.Sum(order => order.Amount);
    var discountPercent = customer!.IsPremium ? 10m : 0m;
    var discount = subtotal * discountPercent / 100m;

    return subtotal - discount;
}

internal sealed record Customer(string Id, string Name, bool IsPremium);

internal sealed record Order(string Number, decimal Amount);
