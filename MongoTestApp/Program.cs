using Microsoft.EntityFrameworkCore;
using MongoDB.Bson;
using MongoDB.Driver;
using Testcontainers.MongoDb;

Guid baxterId;

Console.WriteLine();
Console.WriteLine("Starting the test container:");
Console.WriteLine();

await using var mongoContainer = new MongoDbBuilder()
    .WithImage("mongo:6.0")
    .Build();

await mongoContainer.StartAsync();

Console.WriteLine();
Console.WriteLine("Inserting some documents:");
Console.WriteLine();

var mongoClient = new MongoClient(mongoContainer.GetConnectionString());

await using (var context = new CustomersContext(mongoClient))
{
    var willow = new Customer
    {
        Name = "Willow",
        Species = Species.Dog,
        ContactInfo = new()
        {
            ShippingAddress = new()
            {
                Line1 = "Barking Gate",
                Line2 = "Chalk Road",
                City = "Walpole St Peter",
                Country = "UK",
                PostalCode = "PE14 7QQ"
            },
            BillingAddress = new()
            {
                Line1 = "15a Main St",
                City = "Ailsworth",
                Country = "UK",
                PostalCode = "PE5 7AF"
            },
            Phones = new()
            {
                HomePhone = new() { CountryCode = 44, Number = "7877 555 555" },
                MobilePhone = new() { CountryCode = 1, Number = "(555) 2345-678" },
                WorkPhone = new() { CountryCode = 1, Number = "(555) 2345-678" }
            }
        }
    };
    
    var toast = new Customer
    {
        Name = "Toast",
        Species = Species.Dog,
        ContactInfo = new()
        {
            ShippingAddress = new()
            {
                Line1 = "Barking Gate",
                Line2 = "Chalk Road",
                City = "Walpole St Peter",
                Country = "UK",
                PostalCode = "PE14 7QQ"
            },
            BillingAddress = new()
            {
                Line1 = "15a Main St",
                City = "Ailsworth",
                Country = "UK",
                PostalCode = "PE5 7AF"
            },
            Phones = new()
            {
                HomePhone = new() { CountryCode = 44, Number = "7877 555 555" },
                MobilePhone = new() { CountryCode = 1, Number = "(555) 2345-679" },
                WorkPhone = new() { CountryCode = 1, Number = "(555) 2345-679" }
            }
        }
    };
    
    var baxter = new Customer
    {
        Name = "Baxter",
        Species = Species.Cat,
        ContactInfo = new()
        {
            ShippingAddress = new()
            {
                Line1 = "564 Cat Drive",
                City = "Seattle",
                Country = "USA",
                PostalCode = "98052"
            },
            BillingAddress = new()
            {
                Line1 = "15a Main St",
                City = "Ames",
                Country = "USA",
                PostalCode = "50011"
            },
            Phones = new()
            {
                HomePhone = new() { CountryCode = 1, Number = "(555) 5151 555" },
                MobilePhone = new() { CountryCode = 1, Number = "(555) 2345-666" },
                WorkPhone = new() { CountryCode = 1, Number = "(555) 2345-011" },
            }
        }
    };

    context.AddRange(willow, toast, baxter);
    await context.SaveChangesAsync();

    baxterId = baxter.Id;
}

Console.WriteLine();
Console.WriteLine("Printing the raw BSON documents:");
Console.WriteLine();

await PrintJsonDocuments(mongoClient);

Console.WriteLine();
Console.WriteLine("Querying a single document:");
Console.WriteLine();

using (var context = new CustomersContext(mongoClient))
{
    var name = "Willow";
    var customer = await context.Customers.SingleAsync(c => c.Name == name);

    var address = customer.ContactInfo.ShippingAddress;
    var mobile = customer.ContactInfo.Phones.MobilePhone;
    Console.WriteLine($"{customer.Id}: {customer.Name}");
    Console.WriteLine($"    Shipping to: {address.City}, {address.Country} (+{mobile.CountryCode} {mobile.Number})");
}

Console.WriteLine();
Console.WriteLine("Querying a filtered list of documents:");
Console.WriteLine();

using (var context = new CustomersContext(mongoClient))
{
    var customers = await context.Customers
        .Where(e => e.Species == Species.Dog)
        .ToListAsync();
    
    foreach (var customer in customers)
    {
        PrintCustomer(customer);
    }
}

Console.WriteLine();
Console.WriteLine("Updating a document:");
Console.WriteLine();

using (var context = new CustomersContext(mongoClient))
{
    var baxter = (await context.Customers.FindAsync(baxterId))!;
    baxter.ContactInfo.ShippingAddress = new()
    {
        Line1 = "Via Giovanni Miani",
        City = "Rome",
        Country = "IT",
        PostalCode = "00154"
    };
    
    await context.SaveChangesAsync();
}

using (var context = new CustomersContext(mongoClient))
{
    PrintCustomer((await context.Customers.FindAsync(baxterId))!);
}

async Task PrintJsonDocuments(MongoClient mongoClient)
{
    var collection = mongoClient.GetDatabase("efsample").GetCollection<BsonDocument>("Customers");

    var filter = Builders<BsonDocument>.Filter.Empty;
    var documents = await collection.Find(filter).ToListAsync();
    foreach (var document in documents)
    {
        Console.WriteLine(document);
    }
}

void PrintCustomer(Customer customer)
{
    Console.WriteLine($"{customer.Id}: {customer.Name}");
    var shippingAddress = customer.ContactInfo.ShippingAddress;
    Console.WriteLine($"    Shipping address: {shippingAddress.City}, {shippingAddress.Country}");
    var billingAddress = customer.ContactInfo.BillingAddress;
    Console.WriteLine($"    Billing address: {billingAddress?.City}, {billingAddress?.Country}");
    Console.WriteLine($"    Phones:");
    var homePhone = customer.ContactInfo.Phones.HomePhone;
    Console.WriteLine($"      Home: +{homePhone?.CountryCode} {homePhone?.Number}");
    var workPhone = customer.ContactInfo.Phones.WorkPhone;
    Console.WriteLine($"      Work: +{workPhone?.CountryCode} {workPhone?.Number}");
    var mobilePhone = customer.ContactInfo.Phones.MobilePhone;
    Console.WriteLine($"      Mobile: +{mobilePhone?.CountryCode} {mobilePhone?.Number}");
}

public class CustomersContext : DbContext
{
    private readonly MongoClient _client;

    public CustomersContext(MongoClient client)
    {
        _client = client;
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        => optionsBuilder.UseMongoDB(_client, "efsample");

    public DbSet<Customer> Customers => Set<Customer>();
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
    }
}

public class Customer
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public required Species Species { get; set; }
    public required ContactInfo ContactInfo { get; set; }
}

public class ContactInfo
{
    public required Address ShippingAddress { get; set; }
    public Address? BillingAddress { get; set; }
    public required PhoneNumbers Phones { get; set; }
}

public class PhoneNumbers
{
    public PhoneNumber? HomePhone { get; set; }
    public PhoneNumber? WorkPhone { get; set; }
    public PhoneNumber? MobilePhone { get; set; }
}

public class PhoneNumber
{
    public required int CountryCode { get; set; }
    public required string Number { get; set; }
}

public class Address
{
    public required string Line1 { get; set; }
    public string? Line2 { get; set; }
    public string? Line3 { get; set; }
    public required string City { get; set; }
    public required string Country { get; set; }
    public required string PostalCode { get; set; }
}

public enum Species
{
    Human,
    Dog,
    Cat
}