using EcoChat.Enums;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson.Serialization.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Warehouse = System.Collections.Generic.Dictionary<EcoChat.Enums.Resource, decimal>;

namespace EcoChat.Models
{
	public class Company
	{
		public ObjectId _id { get; set; }
		public int OwnerID { get; set; }
		public string Name { get; set; }
		public int Land { get; set; }

		[BsonDictionaryOptions(DictionaryRepresentation.ArrayOfDocuments)]
		public Dictionary<Guid, Building> Buildings { get; set; }
		[BsonDictionaryOptions(DictionaryRepresentation.ArrayOfDocuments)]
		public Warehouse Warehouse = new Warehouse();

		public decimal WarehouseSize = 1000.0m;

		[BsonIgnore()]
		public StringBuilder CycleReport { get; set; }

		public Company(int ownerId, string name)
		{
			this.OwnerID = ownerId;
			this.Name = name;
			this.WarehouseSize = 1000.0m;
		}

		public void Cycle()
		{
			CycleReport = new StringBuilder();
			foreach (Building building in Buildings.Values)
			{
				building.Cycle(this);
			}

			RemoveAll(Warehouse, (k, v) => v <= 0);
		}

		public static void RemoveAll<K, V>(Dictionary<K, V> dict, Func<K, V, bool> match)
		{
			foreach (var key in dict.Keys.ToArray()
					.Where(key => match(key, dict[key])))
				dict.Remove(key);
		}
	}
}
