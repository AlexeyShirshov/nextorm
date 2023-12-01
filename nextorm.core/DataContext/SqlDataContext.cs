namespace nextorm.core;

// public class SqlDataContext : DataContext
// {
//     public SqlDataContext(DataContextOptionsBuilder optionsBuilder) : base(optionsBuilder)
//     {
//         if (_dataProvider is SqlDataProvider sql)
//         {
//             sql.LogSensetiveData = optionsBuilder.ShouldLogSensetiveData;
//             //sql.CacheQueryCommand=optionsBuilder.CacheQueryCommand;
//         }
//     }
//     protected new SqlDataProvider DataProvider => (SqlDataProvider)_dataProvider;
//     public CommandBuilder From(string table) => new((SqlDataProvider)_dataProvider, table) { Logger = _dataProvider.CommandLogger };
//     //public CommandBuilder<T> Create<T>() => ;
//     public CommandBuilder<T> Create<T>(Action<EntityBuilder<T>>? configEntity = null)
//     {
//         if (!DataProvider.Metadata.ContainsKey(typeof(T)))
//         {
//             var eb = new EntityBuilder<T>();
//             configEntity?.Invoke(eb);
//             DataProvider.Metadata[typeof(T)] = eb.Build();
//         }
//         return new(_dataProvider) { Logger = _dataProvider.CommandLogger };
//     }
// }
