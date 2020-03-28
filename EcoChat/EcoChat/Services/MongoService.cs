using EcoChat.Models;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Driver;

namespace EcoChat.Services
{
	public class MongoService
	{
		private static MongoClient client;
		private static IMongoDatabase db;
		private static IMongoCollection<Player> playerCollection;
		private static IMongoCollection<Company> companyCollection;
		private static IMongoCollection<Market> marketCollection;

		public static void Init()
		{
			// Set up MongoDB conventions
			var pack = new ConventionPack
			{
				new EnumRepresentationConvention(BsonType.String)
			};
			ConventionRegistry.Register("EnumStringConvention", pack, t => true);


			var connectionString = "mongodb://localhost:27017";
			client = new MongoClient(connectionString);
			db = client.GetDatabase("EcoChat_Test");
			try
			{
				db.CreateCollectionAsync("players").Wait();
			}
			catch
			{
			}
			try
			{
				db.CreateCollectionAsync("companies").Wait();
			}
			catch
			{
			}
			try
			{
				db.CreateCollectionAsync("markets").Wait();
			}
			catch
			{
			}

			playerCollection = db.GetCollection<Player>("players");
			companyCollection = db.GetCollection<Company>("companies");
			marketCollection = db.GetCollection<Market>("markets");
		}

		public static IMongoCollection<Player> Players()
		{
			if (client == null)
			{
				Init();
			}
			return playerCollection;
		}

		public static IMongoCollection<Company> Companies()
		{
			if (client == null)
			{
				Init();
			}
			return companyCollection;
		}
		public static Market GetMarket()
		{
			if (client == null)
			{
				Init();
			}
			try
			{
				Market market = marketCollection.Find(o => o.ResourceBalance != null).FirstOrDefault();
				if (market != null)
				{
					//Load our defaults
					market.DefaultResourcePrices = new Market().DefaultResourcePrices;
					return market;
				}
				else
				{
					Market newMarket = new Market();
					SetMarket(newMarket);
					return newMarket;
				}
			}
			catch
			{
				Market newMarket = new Market();
				SetMarket(newMarket);
				return newMarket;
			}
		}

		public static void SetMarket(Market market)
		{
			try
			{
				marketCollection.DeleteMany(o => o.ResourceBalance != null);
			}
			catch
			{
			}
			marketCollection.InsertOne(market);
		}
	}
}
