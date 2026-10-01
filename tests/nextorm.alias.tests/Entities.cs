namespace NextORM.AliasTests;

/// <summary>Entity whose <see cref="BuyerId"/> and <see cref="ApproverId"/> both point at <see cref="Person"/>.</summary>
public sealed class Order
{
    public int Id { get; set; }

    public int BuyerId { get; set; }

    public int ApproverId { get; set; }
}

/// <summary>Entity joined twice (as buyer and as approver); the CLR type repeats in one projection.</summary>
public sealed class Person
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;
}
