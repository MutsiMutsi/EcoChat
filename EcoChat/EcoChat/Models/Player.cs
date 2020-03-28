using MongoDB.Bson.Serialization.Attributes;

namespace EcoChat.Models
{
	[BsonIgnoreExtraElements]
	public class Player
	{
		public int UserID { get; set; }
		public string Name { get; set; }
		public decimal Money { get; set; }
	}
}
