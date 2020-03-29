using EcoChat.Enums;
using EcoChat.Models;
using EcoChat.Services;
using Hangfire;
using Hangfire.MemoryStorage;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Driver;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Args;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace EcoChat
{
	class Program
	{
		private const long MAINCHATID = -1001201035730;

		private static TelegramBotClient botClient;
		private static Dictionary<string, int> usernames = new Dictionary<string, int>();
		private static Random rng = new Random();
		private static List<int> currentEpochUserAction = new List<int>();

		private static Dictionary<int, int> userRequests = new Dictionary<int, int>();

		private static Market market = null;

		private static string GetUsername(int userid, long chatid)
		{
			var chat = botClient.GetChatMemberAsync(chatid, userid).GetAwaiter().GetResult();
			return chat.User.Username;
		}

		private static string GetUserID(long chatid, string username)
		{
			var chat = botClient.GetChatAsync(chatid).GetAwaiter().GetResult();
			return "";
		}

		static void Main(string[] args)
		{
			market = MongoService.GetMarket();
			Market newMarket = new Market();
			foreach (var marketPrice in newMarket.DefaultResourcePrices)
			{
				if (!market.DefaultResourcePrices.ContainsKey(marketPrice.Key))
				{
					market.DefaultResourcePrices.Add(marketPrice.Key, marketPrice.Value);
					market.ResourceBalance.Add(marketPrice.Key, 0);
				}
				else
					market.DefaultResourcePrices[marketPrice.Key] = marketPrice.Value;
			}

			var cc = GetAllCompanies();
			foreach (var c in cc)
			{
				c.Buildings.Values.ToList().ForEach(o =>
				{
					BuildingType bType = BuildingType.Factory;
					foreach (var key in Enum.GetNames(typeof(BuildingType)))
					{
						BuildingType iter = (BuildingType)Enum.Parse(typeof(BuildingType), key);
						var updatedProd = BuildingService.GetBuildingProductionDefs(iter).Where(j => j.Output.FirstOrDefault().Key == o.Production.Output.First().Key).FirstOrDefault();
						if (updatedProd != null)
						{
							bType = iter;
							o.Production = updatedProd;
						}
					}
					o.Type = bType;
				});

				SaveCompany(c);
			}

			InitializeBot();
		}

		static void updateUsernames(long chatid)
		{
			usernames.Clear();
			IEnumerable<int> userids = MongoService.Players().Find(o => o.UserID != 0).ToList().Select(o => o.UserID);
			foreach (int userid in userids)
			{
				string username = botClient.GetChatMemberAsync(chatid, userid).GetAwaiter().GetResult().User.Username;
				usernames.Add(username, userid);
			}
		}

		static void InitializeBot()
		{
			botClient = new Telegram.Bot.TelegramBotClient(ApiConfig.TG_API_KEY);
			botClient.OnMessage += BotClient_OnMessage;
			botClient.OnCallbackQuery += BotOnCallbackQueryReceived;
			botClient.OnInlineResultChosen += BotOnChosenInlineResultReceived;
			botClient.StartReceiving();

			//Initialize Hangfire
			GlobalConfiguration.Configuration.UseMemoryStorage();
			RecurringJob.AddOrUpdate(() => DailyReport(), "* * * * *", TimeZoneInfo.Utc);

			using (new BackgroundJobServer())
			{
				Console.WriteLine("Hangfire Server started. Press ENTER to exit...");
				Console.ReadLine();
			}
		}

		public static void DailyReport()
		{
			currentEpochUserAction.Clear();
			market.Update(rng);
			MongoService.SetMarket(market);

			StringBuilder msg = new StringBuilder();
			msg.AppendLine("*-Daily Report-*");
			msg.AppendLine();
			msg.AppendLine("*-Price Report-*");
			/*foreach (var price in ResourcePrices)
			{
				msg.AppendLine($"{Enum.GetName(typeof(Resource), price.Key)} S:`{price.Value}` B:`{Math.Round((double)ResourcePrices[price.Key] * 1.25, 2)}`");
			}*/

			foreach (Resource res in Enum.GetValues(typeof(Resource)).OfType<Resource>().OrderBy(o => o.ToString()))
			{
				msg.AppendLine($"{res} `{market.GetPrice(res)}` - `{Math.Round((double)market.GetPrice(res) * 1.25, 2)}`");
			}

			msg.AppendLine();
			msg.AppendLine("*-Company Reports-*");
			IEnumerable<Company> companies = GetAllCompanies();
			foreach (Company company in companies)
			{
				company.Cycle();
				if (company.CycleReport.Length > 0)
				{
					msg.AppendLine(company.Name);
					msg.AppendLine(company.CycleReport.ToString());
				}
				SaveCompany(company);
			}
			botClient.SendTextMessageAsync(MAINCHATID, msg.ToString().Replace("_", "\\_"), Telegram.Bot.Types.Enums.ParseMode.Markdown).GetAwaiter().GetResult();
		}

		private static async void BotOnCallbackQueryReceived(object sender, CallbackQueryEventArgs callbackQueryEventArgs)
		{
			try
			{
				var callbackQuery = callbackQueryEventArgs.CallbackQuery;
				if (!userRequests.ContainsKey(callbackQuery.Message.MessageId))
					return;
				int requester = userRequests[callbackQuery.Message.MessageId];
				userRequests.Remove(callbackQuery.Message.MessageId);

				if (callbackQuery.From.Id != requester)
				{
					await botClient.AnswerCallbackQueryAsync(
						callbackQueryId: callbackQuery.Id,
						text: "Not enough money"
					);
					return;
				}

				string reply = "";
				if (callbackQuery.Message.Text == "Which company would you like to upgrade")
				{
					reply = upgradeCompany(callbackQuery.Data);
				}
				if (callbackQuery.Message.Text == "Which building do you want to delete? (caution irreversible)")
				{
					Company c = GetUserCompanies(callbackQuery.From.Id).FirstOrDefault();
					Guid buildingKey = Guid.Parse(callbackQuery.Data);
					c.Buildings.Remove(buildingKey);
					reply = "Building deleted!";
					SaveCompany(c);
				}
				if (callbackQuery.Message.Text == "Configure building")
				{
					Company c = GetUserCompanies(callbackQuery.From.Id).FirstOrDefault();
					string guidStr = callbackQuery.Data.Split(':')[0];
					string resStr = callbackQuery.Data.Split(':')[1];

					Building b = c.Buildings[Guid.Parse(guidStr)];
					ProductionDefinition pDef = null;
					if (resStr == "null")
					{
						b.Operational = false;
						reply = "Building configured";
					}
					else if (resStr == "upgrade")
					{
						decimal cost = (decimal)Math.Pow(b.Tier, 3);
						if (!c.Warehouse.ContainsKey(Resource.Steel))
						{
							reply = $"Can not upgrade not enough '{Resource.Steel} {cost}'.";
						}
						else if (c.Warehouse[Resource.Steel] < cost)
						{
							reply = $"Can not upgrade not enough '{Resource.Steel} {cost}'.";
						}
						else
						{
							b.Tier++;
							c.Warehouse[Resource.Steel] -= cost;
							reply = "Building upgraded!";
						}
					}
					else
					{
						pDef = BuildingService.GetBuildingProductionDefs(b.Type).First(o => o.Output.First().Key.ToString() == resStr);
						b.Operational = true;
						b.Production = pDef;
						reply = "Building configured";
					}
					SaveCompany(c);

				}
				if (callbackQuery.Message.Text == "Select building")
				{
					Company c = GetUserCompanies(callbackQuery.From.Id).FirstOrDefault();
					Building b = c.Buildings[Guid.Parse(callbackQuery.Data)];

					var resTypes = BuildingService.GetBuildingProductionDefs(b.Type);
					int numRes = resTypes.Count() + 1;
					List<List<InlineKeyboardButton>> buttons = new List<List<InlineKeyboardButton>>();

					buttons.Add(new List<InlineKeyboardButton> { InlineKeyboardButton.WithCallbackData("Inactivate", Guid.Parse(callbackQuery.Data).ToString() + ":" + "null") });
					buttons.Add(new List<InlineKeyboardButton> { InlineKeyboardButton.WithCallbackData("Upgrade", Guid.Parse(callbackQuery.Data).ToString() + ":" + "upgrade") });
					foreach (var res in resTypes)
					{
						buttons.Add(new List<InlineKeyboardButton>{
								InlineKeyboardButton.WithCallbackData($"Produce: {res.Output.First().Key.ToString()}", Guid.Parse(callbackQuery.Data).ToString() + ":" + res.Output.First().Key.ToString())
								});
					}
					var inlineKeyboard = new InlineKeyboardMarkup(buttons);

					int requestID = botClient.SendTextMessageAsync(
						chatId: callbackQuery.Message.Chat.Id,
						text: $"Configure building",
						replyMarkup: inlineKeyboard
					).GetAwaiter().GetResult().MessageId;

					userRequests.Add(requestID, callbackQuery.From.Id);
				}
				if (callbackQuery.Message.Text == "What kind of building would you like to construct?")
				{
					BuildingType resType = (BuildingType)Enum.Parse(typeof(BuildingType), callbackQuery.Data);
					//TODO: Add multi company support
					Company c = GetUserCompanies(callbackQuery.From.Id).FirstOrDefault();

					var quote = BuildingService.QuoteBuilding(c, resType);
					bool hasResources = true;
					foreach (var cost in quote)
					{
						if (!c.Warehouse.ContainsKey(cost.Key))
						{
							reply = $"Can not build not enough '{cost.Key} {cost.Value}'.";
							hasResources = false;
						}
						else if (c.Warehouse[cost.Key] < cost.Value)
						{
							reply = $"Can not build not enough '{cost.Key} {cost.Value}'.";
							hasResources = false;
						}
					}
					if (hasResources)
					{
						Building newBuilding = BuildingService.CreateBuilding(c, resType);
						c.Buildings.Add(Guid.NewGuid(), newBuilding);
						reply = $"{callbackQuery.From.Username} created a new {callbackQuery.Data} building!";

						foreach (var cost in quote)
							c.Warehouse[cost.Key] -= cost.Value;

						SaveCompany(c);
					}
				}

				if (!string.IsNullOrEmpty(reply))
				{
					await botClient.AnswerCallbackQueryAsync(
						callbackQueryId: callbackQuery.Id,
						text: reply
					);

					await botClient.SendTextMessageAsync(
						chatId: callbackQuery.Message.Chat.Id,
						text: reply
					);
				}

				await botClient.DeleteMessageAsync(new ChatId(callbackQuery.Message.Chat.Id), callbackQuery.Message.MessageId);
			}
			catch (Exception e)
			{
				Console.WriteLine(e.Message);
			}

		}

		private static void BotOnChosenInlineResultReceived(object sender, ChosenInlineResultEventArgs chosenInlineResultEventArgs)
		{
			try
			{
				Console.WriteLine($"Received inline result: {JsonConvert.SerializeObject(chosenInlineResultEventArgs.ChosenInlineResult)}");
			}
			catch
			{

				throw;
			}
		}

		private static Player GetPlayer(int userid)
		{
			return MongoService.Players().Find(o => o.UserID == userid).FirstOrDefault();
		}
		private static void SavePlayer(Player player)
		{
			MongoService.Players().ReplaceOne(o => o.UserID == player.UserID, player);
		}
		private static void SaveCompany(Company company)
		{
			MongoService.Companies().ReplaceOne(o => o._id == company._id, company);
		}
		private static Company GetCompany(string companyId)
		{
			return MongoService.Companies().Find(o => o._id == ObjectId.Parse(companyId)).FirstOrDefault();
		}
		private static IEnumerable<Company> GetUserCompanies(int userid)
		{
			return MongoService.Companies().Find(o => o.OwnerID == userid).ToEnumerable();
		}
		private static IEnumerable<Company> GetAllCompanies()
		{
			return MongoService.Companies().Find(o => o.OwnerID != 0).ToEnumerable();
		}

		private static void BotClient_OnMessage(object sender, MessageEventArgs e)
		{
			try
			{
				if (e.Message.Text == null)
					return;

				string msg = e.Message.Text;
				if (msg.StartsWith("?"))
				{
					string cmd = msg.ToLower().Substring(1, msg.Length - 1);
					if (cmd == "production")
					{
						StringBuilder info = new StringBuilder();
						foreach (Resource res in Enum.GetValues(typeof(Resource)))
						{
							ProductionDefinition def = BuildingService.GetProductionDefinition(res);
							info.AppendLine();
							info.AppendLine($"*{res.ToString()}*");
							//Write 
							if (def.Input != null)
							{
								info.Append("Input: ");
								foreach (var input in def.Input)
								{
									info.Append($"{input.Key} `{input.Value}` ");
								}
								info.AppendLine();
							}
							//Write outputs
							if (def.Output != null)
							{
								info.Append("Output: ");
								foreach (var output in def.Output)
								{
									info.Append($"{output.Key} `{output.Value}`");
								}
								info.AppendLine();
							}
						}
						botClient.SendTextMessageAsync(e.Message.Chat.Id, info.ToString(), Telegram.Bot.Types.Enums.ParseMode.Markdown);
					}
				}
				if (msg.StartsWith("$"))
				{
					string cmd = msg.ToLower().Substring(1, msg.Length - 1);
					Console.WriteLine($"{e.Message.From.Username} in {e.Message.Chat.Title} : {e.Message.Text}");
					string reply = "";

					if (cmd == "register")
					{
						reply = registerUser(e);
					}
					else if (cmd == "cash")
					{
						reply = cash(e);
					}
					else if (cmd.StartsWith("found"))
					{
						string companyName = msg.Remove(0, "$found ".Length);
						reply = foundCompany(e.Message.From.Id, companyName);
					}
					else if (cmd.StartsWith("contracts"))
					{
						updateUsernames(e.Message.Chat.Id);
						Player p = GetPlayer(e.Message.From.Id);
						Dictionary<Guid, Contract> outgoing = market.Contracts.Where(o => o.Value.Sender == e.Message.From.Id).ToDictionary(item => item.Key, item => item.Value);
						Dictionary<Guid, Contract> incoming = market.Contracts.Where(o => o.Value.Receiver == e.Message.From.Id).ToDictionary(item => item.Key, item => item.Value);
						StringBuilder sb = new StringBuilder();

						sb.AppendLine("*Outgoing Offers*");
						foreach (Contract c in outgoing.Values)
						{
							string receiverName = GetUsername(c.Receiver, e.Message.Chat.Id);
							sb.AppendLine($@"`({c.ID.ToString().Substring(0, 3)})`{c.Amount} {c.Resource} for `{c.Price}` total `{c.Total}` to {receiverName}");
						}
						sb.AppendLine("*Incoming Requests*");
						foreach (Contract c in incoming.Values)
						{
							string senderName = GetUsername(c.Sender, e.Message.Chat.Id);
							sb.AppendLine($@"`({c.ID.ToString().Substring(0, 3)})`{c.Amount} {c.Resource} for `{c.Price}` total `{c.Total}` from {senderName}");
						}
						reply = sb.ToString();
					}
					else if (cmd.StartsWith("cancel"))
					{
						string[] split = cmd.Split(null);
						string id = split[1];

						Contract c = market.Contracts.FirstOrDefault(o => o.Value.Sender == e.Message.From.Id && o.Key.ToString().StartsWith(id)).Value;
						Player pSender = GetPlayer(c.Sender);
						Company cSender = GetUserCompanies(c.Sender).FirstOrDefault();

						if (cSender.Warehouse.ContainsKey(c.Resource))
						{
							cSender.Warehouse[c.Resource] += c.Amount;
						}
						else
						{
							cSender.Warehouse.Add(c.Resource, c.Amount);
						}
						reply = $"Contract cancelled";
						SavePlayer(pSender);
						SaveCompany(cSender);
						market.Contracts.Remove(c.ID);
					}
					else if (cmd.StartsWith("reject"))
					{
						string[] split = cmd.Split(null);
						string id = split[1];

						Contract c = market.Contracts.FirstOrDefault(o => o.Value.Receiver == e.Message.From.Id && o.Key.ToString().StartsWith(id)).Value;
						Player pSender = GetPlayer(c.Sender);
						Company cSender = GetUserCompanies(c.Sender).FirstOrDefault();

						if (cSender.Warehouse.ContainsKey(c.Resource))
						{
							cSender.Warehouse[c.Resource] += c.Amount;
						}
						else
						{
							cSender.Warehouse.Add(c.Resource, c.Amount);
						}
						reply = $"Contract rejected";
						SavePlayer(pSender);
						SaveCompany(cSender);
						market.Contracts.Remove(c.ID);
					}
					else if (cmd.StartsWith("accept"))
					{
						string[] split = cmd.Split(null);
						string id = split[1];

						Contract c = market.Contracts.FirstOrDefault(o => o.Value.Receiver == e.Message.From.Id && o.Key.ToString().StartsWith(id)).Value;
						Player pSender = GetPlayer(c.Sender);
						Player pReceiver = GetPlayer(c.Receiver);
						Company cReceiver = GetUserCompanies(c.Receiver).FirstOrDefault();

						pSender.Money += c.Total;
						pReceiver.Money -= c.Total;
						if (cReceiver.Warehouse.ContainsKey(c.Resource))
						{
							cReceiver.Warehouse[c.Resource] += c.Amount;
						}
						else
						{
							cReceiver.Warehouse.Add(c.Resource, c.Amount);
						}
						SavePlayer(pSender);
						SavePlayer(pReceiver);
						SaveCompany(cReceiver);
						market.Contracts.Remove(c.ID);
						reply = $"Contract accepted, received `{c.Amount}` {c.Resource}";
					}
					else if (cmd.StartsWith("offer"))
					{
						Player p = GetPlayer(e.Message.From.Id);
						Company c = GetUserCompanies(e.Message.From.Id).FirstOrDefault();
						string[] split = cmd.Split(null);
						string[] oSplit = msg.Substring(1, msg.Length - 1).Split(null);

						try
						{
							string toUsername = oSplit[1];
							string resString = split[2];
							resString = char.ToUpper(resString[0]) + resString.Substring(1);
							Resource res = (Resource)Enum.Parse(typeof(Resource), resString);
							decimal amount = decimal.Parse(split[3]);
							decimal price = decimal.Parse(split[4]);

							if (!c.Warehouse.ContainsKey(res) || c.Warehouse[res] < amount)
							{
								reply = $"Not enough {res} to offer";
							}
							else
							{
								updateUsernames(e.Message.Chat.Id);
								int toUser = usernames[toUsername.Replace("@", "")];



								Contract newContract = new Contract
								{
									Amount = amount,
									Length = 1,
									ID = Guid.NewGuid(),
									Price = price,
									Receiver = toUser,
									Resource = res,
									Sender = e.Message.From.Id
								};

								c.Warehouse[res] -= amount;

								market.Contracts.Add(newContract.ID, newContract);
								reply = $@"Contract created - {newContract.Amount} {newContract.Resource} for `{newContract.Price}` total `{newContract.Total}`";
								SaveCompany(c);
							}
						}
						catch (Exception)
						{

							throw;
						}
					}
					else if (cmd == "warehouse")
					{
						Company c = GetUserCompanies(e.Message.From.Id).FirstOrDefault();
						if (c == null)
							reply = "you have no company";
						else
						{
							StringBuilder sb = new StringBuilder();
							if (c.Warehouse != null)
							{
								sb.AppendLine($"*{e.Message.From.Username} warehouse*");
								foreach (var resource in c.Warehouse)
								{
									sb.AppendLine($"{resource.Key} : `{resource.Value}`");
								}
							}
							reply = sb.ToString();
						}
					}
					else if (cmd.StartsWith("buy"))
					{
						string[] split = cmd.Split(null);
						Player p = GetPlayer(e.Message.From.Id);
						Company c = GetUserCompanies(e.Message.From.Id).FirstOrDefault();

						try
						{
							string resString = split[1];
							resString = char.ToUpper(resString[0]) + resString.Substring(1);
							Resource res = (Resource)Enum.Parse(typeof(Resource), resString);
							//decimal price = ResourcePrices[res] * 1.25m;
							decimal price = market.GetPrice(res) * 1.25m;

							if (res == Resource.Machinery)
								reply = "Cant buy non-raw resources from global market";
							else
							{
								decimal amount = 0.0m;
								if (split[2] == "max" || split[2] == "all")
									amount = (int)(p.Money / price);
								else
									amount = decimal.Parse(split[2]);

								if (p.Money < price * amount)
									reply = $"Not enough money to buy {amount} {res} for `{price * amount}`";
								else
								{
									p.Money -= price * amount;
									SavePlayer(p);

									if (!c.Warehouse.ContainsKey(res))
										c.Warehouse.Add(res, amount);
									else
										c.Warehouse[res] += amount;

									market.ResourceBalance[res] += amount;
									reply = $"Bought {amount} {res} for `{price * amount}`";
									SaveCompany(c);
								}
							}
						}
						catch
						{
							reply = "Invalid purchase";
						}
					}
					else if (cmd.StartsWith("sell"))
					{
						string[] split = cmd.Split(null);
						Player p = GetPlayer(e.Message.From.Id);
						Company c = GetUserCompanies(e.Message.From.Id).FirstOrDefault();

						try
						{
							if (split[1] == "warehouse")
							{
								StringBuilder sbReply = new StringBuilder();
								var keys = c.Warehouse.Keys.ToList();
								foreach (var key in keys)
								{
									decimal price = market.GetPrice(key);
									decimal amount = c.Warehouse[key];
									amount = (int)amount;
									sbReply.AppendLine(SellFromWarehouse(p, c, key, price, amount));
								}
								reply = sbReply.ToString();
							}
							else
							{

								string resString = split[1];
								resString = char.ToUpper(resString[0]) + resString.Substring(1);
								Resource res = (Resource)Enum.Parse(typeof(Resource), resString);
								//decimal price = ResourcePrices[res];
								decimal price = market.GetPrice(res);

								decimal amount = 0.0m;
								if (split[2] == "max" || split[2] == "all")
									amount = c.Warehouse[res];
								else
									amount = decimal.Parse(split[2]);

								amount = (int)amount;
								reply = SellFromWarehouse(p, c, res, price, amount);
							}
						}
						catch
						{
							reply = "Invalid sale";
						}
					}
					else if (cmd == "expand")
					{
						Player p = GetPlayer(e.Message.From.Id);
						Company c = GetUserCompanies(e.Message.From.Id).FirstOrDefault();

						decimal expandCost = 1000000 * (c.Land + 1);
						if (p.Money > expandCost)
						{
							reply = $"Company expandend for `{expandCost}`$";
							p.Money -= expandCost;
							c.Land++;
							SavePlayer(p);
							SaveCompany(c);
						}
						else
						{
							reply = $"Not enough money need `{expandCost}` cash";
						}
					}
					else if (cmd == "build")
					{
						botClient.SendChatActionAsync(e.Message.Chat.Id, Telegram.Bot.Types.Enums.ChatAction.Typing);

						Company c = GetUserCompanies(e.Message.From.Id).FirstOrDefault();

						if (c.Buildings.Count == 5 + c.Land)
							throw new Exception("No more space for buildings buy more land for 1,000,000.- cash (unimplemented)");


						/*int numRes = Enum.GetNames(typeof(Resource)).Length;
						InlineKeyboardButton[] buttons = new InlineKeyboardButton[numRes];
						int iter = 0;
						foreach (Resource res in Enum.GetValues(typeof(Resource)))
						{
							buttons[iter] = InlineKeyboardButton.WithCallbackData(res.ToString());
							iter++;
						}*/

						List<List<InlineKeyboardButton>> buttons = new List<List<InlineKeyboardButton>>();

						var bTypes = Enum.GetValues(typeof(BuildingType));
						foreach (BuildingType bType in bTypes)
						{
							buttons.Add(new List<InlineKeyboardButton>{
								InlineKeyboardButton.WithCallbackData(bType.ToString())
								});
						}
						/*if (c.Buildings.Count == 5)
						{
							foreach (Building b in c.Buildings.Values)
							{
								buttons.Add(new List<InlineKeyboardButton>{
								InlineKeyboardButton.WithCallbackData(b.Production.Output.FirstOrDefault().Key.ToString())
								});
							}
						}
						else
						{
							foreach (Resource res in Enum.GetValues(typeof(Resource)))
							{
								buttons.Add(new List<InlineKeyboardButton>{
								InlineKeyboardButton.WithCallbackData(res.ToString())
							});
							}
						}*/

						var inlineKeyboard = new InlineKeyboardMarkup(buttons);

						int requestID = botClient.SendTextMessageAsync(
							chatId: e.Message.Chat.Id,
							text: $"What kind of building would you like to construct?",
							replyMarkup: inlineKeyboard
						).GetAwaiter().GetResult().MessageId;

						userRequests.Add(requestID, e.Message.From.Id);
					}
					else if (cmd == "buildings")
					{
						botClient.SendChatActionAsync(e.Message.Chat.Id, Telegram.Bot.Types.Enums.ChatAction.Typing);
						Company c = GetUserCompanies(e.Message.From.Id).FirstOrDefault();

						List<List<InlineKeyboardButton>> buttons = new List<List<InlineKeyboardButton>>();
						StringBuilder sb = new StringBuilder();
						foreach (var kvp in c.Buildings)
						{
							Building b = kvp.Value;
							string output = "none";
							if (b.Production != null && b.Production.Output != null)
							{
								output = b.Production.Output.First().Key.ToString();
							}
							buttons.Add(new List<InlineKeyboardButton>{
								InlineKeyboardButton.WithCallbackData($"{b.Type}(T{b.Tier}) - {output}", kvp.Key.ToString())
								});
						}

						var inlineKeyboard = new InlineKeyboardMarkup(buttons);

						int requestID = botClient.SendTextMessageAsync(
							chatId: e.Message.Chat.Id,
							text: $"Select building",
							replyMarkup: inlineKeyboard
						).GetAwaiter().GetResult().MessageId;

						userRequests.Add(requestID, e.Message.From.Id);
					}
					else if (cmd == "delete")
					{
						botClient.SendChatActionAsync(e.Message.Chat.Id, Telegram.Bot.Types.Enums.ChatAction.Typing);

						Company c = GetUserCompanies(e.Message.From.Id).FirstOrDefault();

						/*int numRes = Enum.GetNames(typeof(Resource)).Length;
						InlineKeyboardButton[] buttons = new InlineKeyboardButton[numRes];
						int iter = 0;
						foreach (Resource res in Enum.GetValues(typeof(Resource)))
						{
							buttons[iter] = InlineKeyboardButton.WithCallbackData(res.ToString());
							iter++;
						}*/

						List<List<InlineKeyboardButton>> buttons = new List<List<InlineKeyboardButton>>();

						var resOptions = Enum.GetValues(typeof(Resource));
						foreach (var kvp in c.Buildings)
						{
							Building b = kvp.Value;
							buttons.Add(new List<InlineKeyboardButton>{
								InlineKeyboardButton.WithCallbackData($"{b.Type}(T{b.Tier})", kvp.Key.ToString())
								});
						}

						var inlineKeyboard = new InlineKeyboardMarkup(buttons);

						int requestID = botClient.SendTextMessageAsync(
							chatId: e.Message.Chat.Id,
							text: $"Which building do you want to delete? (caution irreversible)",
							replyMarkup: inlineKeyboard
						).GetAwaiter().GetResult().MessageId;

						userRequests.Add(requestID, e.Message.From.Id);
					}
					else if (cmd == "table")
					{

					}
					else if (cmd == "balance")
					{
						Company c = GetUserCompanies(e.Message.From.Id).FirstOrDefault();
						StringBuilder sb = new StringBuilder();

						Dictionary<Resource, decimal> balance = new Dictionary<Resource, decimal>();
						foreach (var building in c.Buildings)
						{
							if (building.Value.Production == null || !building.Value.Operational)
								continue;
							if (building.Value.Production.Input != null)
							{
								foreach (var input in building.Value.Production.Input)
								{
									decimal val = input.Value * building.Value.Tier;
									if (balance.ContainsKey(input.Key))
										balance[input.Key] -= val;
									else
										balance.Add(input.Key, -val);
								}
							}
							foreach (var output in building.Value.Production.Output)
							{
								decimal val = output.Value * building.Value.Tier;
								if (balance.ContainsKey(output.Key))
									balance[output.Key] += val;
								else
									balance.Add(output.Key, val);
							}
						}

						sb.AppendLine($"*{e.Message.From.Username} daily balance*");
						foreach (var bItem in balance)
						{
							sb.AppendLine($"{bItem.Key} : `{bItem.Value}`");
						}

						reply = sb.ToString();
					}
					else if (cmd == "marketreport")
					{
						reply = market.BalanceReport();
					}
					else if (cmd == "upgrade")
					{
						/*botClient.SendChatActionAsync(e.Message.Chat.Id, Telegram.Bot.Types.Enums.ChatAction.Typing);

						IEnumerable<Company> existing = GetUserCompanies(e.Message.From.Id);

						List<List<InlineKeyboardButton>> buttons = new List<List<InlineKeyboardButton>>();
						foreach (Company comp in existing)
						{
							decimal upgradeCost = (decimal)Math.Pow(comp.Tier, 3) * 2500.0m;
							buttons.Add(new List<InlineKeyboardButton>{
								InlineKeyboardButton.WithCallbackData($"{comp.Name} T{comp.Tier} => T{comp.Tier + 1} ${upgradeCost}", comp._id.ToString())
							});
						}
						var inlineKeyboard = new InlineKeyboardMarkup(buttons);

						int requestID = botClient.SendTextMessageAsync(
							chatId: e.Message.Chat.Id,
							text: $"Which company would you like to upgrade",
							replyMarkup: inlineKeyboard
						).GetAwaiter().GetResult().MessageId;

						userRequests.Add(requestID, e.Message.From.Id);*/
						reply = "Building upgrading is currently unavailable";
					}
					else if (currentEpochUserAction.Contains(e.Message.From.Id))
					{
						reply = $"{e.Message.From.Username} you already performed your daily action";
					}
					else
					{
						if (cmd == "beg")
						{
							reply = beg(e);
							currentEpochUserAction.Add(e.Message.From.Id);
						}
						if (cmd == "work")
						{
							reply = work(e);
							currentEpochUserAction.Add(e.Message.From.Id);
						}
					}

					if (!string.IsNullOrWhiteSpace(reply))
						botClient.SendTextMessageAsync(e.Message.Chat.Id, reply.Replace("_", "\\_"), Telegram.Bot.Types.Enums.ParseMode.Markdown).GetAwaiter().GetResult();
				}
			}
			catch (Exception ex)
			{
				botClient.SendTextMessageAsync(e.Message.Chat.Id, e.Message.Text + ex.Message, Telegram.Bot.Types.Enums.ParseMode.Default).GetAwaiter().GetResult();
				botClient.SendTextMessageAsync(e.Message.Chat.Id, JsonConvert.SerializeObject(ex.StackTrace), Telegram.Bot.Types.Enums.ParseMode.Default).GetAwaiter().GetResult();
			}
		}

		private static string SellFromWarehouse(Player p, Company c, Resource res, decimal price, decimal amount)
		{
			string reply;
			if (!c.Warehouse.ContainsKey(res) || c.Warehouse[res] < amount)
				reply = $"Not enough {res}";
			else
			{
				p.Money += price * amount;
				SavePlayer(p);
				c.Warehouse[res] -= amount;

				market.ResourceBalance[res] -= amount;
				reply = $"Sold {amount} {res} for `{price * amount}`";
				SaveCompany(c);
			}

			return reply;
		}

		private static string cash(MessageEventArgs e)
		{
			string reply;
			Player player = MongoService.Players().Find(o => o.UserID == e.Message.From.Id).FirstOrDefault();
			reply = $"{player.Name} has `${player.Money}`";
			return reply;
		}

		private static string beg(MessageEventArgs e)
		{
			string reply;

			var resses = Enum.GetValues(typeof(Resource));
			int index = rng.Next(0, resses.Length);
			Resource selected = resses.OfType<Resource>().ToList()[index];

			decimal priceWeight = 1.0m / market.DefaultResourcePrices[selected];
			decimal luckyWeight = (decimal)rng.Next(0, 100);
			decimal quantity = Math.Round(priceWeight * luckyWeight, 0);


			Company c = GetUserCompanies(e.Message.From.Id).FirstOrDefault();

			if (c.Warehouse.ContainsKey(selected))
			{
				c.Warehouse[selected] += quantity;
			}
			else
			{
				c.Warehouse.Add(selected, quantity);
			}
			reply = $"You received '{selected} {quantity}'.";

			SaveCompany(c);

			return reply;
		}

		private static string work(MessageEventArgs e)
		{
			string reply;
			double amount = rng.NextDouble() * 100.0;
			amount = Math.Round(amount, 2);
			reply = $"{e.Message.From.Username} got `${amount}` great job";
			Player player = GetPlayer(e.Message.From.Id);
			player.Money += (decimal)amount;
			if (player.Money < 0)
				player.Money = 0;
			SavePlayer(player);

			return reply;
		}

		private static string registerUser(MessageEventArgs e)
		{
			string reply;
			bool playerExists = GetPlayer(e.Message.From.Id) != null;
			reply = $"Player {e.Message.From.Username} is already in the game!";

			if (!playerExists)
			{
				MongoService.Players().InsertOne(new Models.Player
				{
					Money = 0.0m,
					Name = e.Message.From.Username,
					UserID = e.Message.From.Id
				});
				reply = $"Player {e.Message.From.Username} added to the game!";
			}

			return reply;
		}

		private static string foundCompany(int userid, string companyName)
		{
			if (string.IsNullOrEmpty(companyName.Trim()))
				return "invalid name use $found company name";

			string reply;
			Player player = MongoService.Players().Find(o => o.UserID == userid).FirstOrDefault();
			reply = $"Player {player.Name} doesnt exist!";

			IEnumerable<Company> existing = GetUserCompanies(player.UserID);
			int numCompany = existing.Count();

			if (player != null)
			{
				if (existing.Count() == 0)
				{
					Company company = new Company(player.UserID, companyName);
					company.Buildings = new Dictionary<Guid, Building>();
					//Building newBuilding = BuildingService.CreateBuilding(company, Resource.Electricity);
					//company.Buildings.Add(Guid.NewGuid(), newBuilding);

					reply = $"Player {player.Name} founded {company.Name}!";

					player.Money = 0;
					company.Warehouse = new Dictionary<Resource, decimal>
					{
						{ Resource.Iron, 300},
						{ Resource.Wood, 750},
						{ Resource.Electricity, 10000},
						{ Resource.Water, 50000},
					};
					MongoService.Companies().InsertOne(company);
					SavePlayer(player);
				}
				else
				{
					reply = $"Freemium members can only own one company";
				}
			}

			return reply;
		}

		private static string upgradeCompany(string companyid)
		{
			/*string reply = "";
			Company company = GetCompany(companyid);
			Player player = MongoService.Players().Find(o => o.UserID == company.OwnerID).FirstOrDefault();

			decimal upgradeCost = (decimal)Math.Pow(company.Tier, 3) * 2500.0m;

			if (player != null)
			{
				if (player.Money > upgradeCost)
				{
					reply = $"Player {player.Name} upgraded {company.Name}!";
					player.Money -= upgradeCost;
					company.Tier++;
					SaveCompany(company);
					SavePlayer(player);
				}
				else
				{
					reply = $"Not enough money you need ${upgradeCost} to upgrade {company.Name}";
				}
			}

			return reply;*/

			return "unavailable";
		}
	}
}
