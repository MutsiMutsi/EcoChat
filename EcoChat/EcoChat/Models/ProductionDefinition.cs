using EcoChat.Enums;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson.Serialization.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EcoChat.Models
{
	public class ProductionDefinition
	{
		[BsonDictionaryOptions(DictionaryRepresentation.ArrayOfDocuments)]
		public Dictionary<Resource, decimal> Input { get; set; }
		[BsonDictionaryOptions(DictionaryRepresentation.ArrayOfDocuments)]
		public Dictionary<Resource, decimal> Output { get; set; }
		public decimal Upkeep { get; set; }
	}
}
