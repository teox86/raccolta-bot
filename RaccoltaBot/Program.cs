using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Timers;
using Telegram.Bot;
using Telegram.Bot.Args;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.InlineQueryResults;
using Telegram.Bot.Types.ReplyMarkups;

namespace RaccoltaBot
{
    public static class Program
    {
        private static readonly TelegramBotClient Bot = new TelegramBotClient(Token.Key);

        private static readonly string UsersFile = System.IO.Path.GetDirectoryName(Assembly.GetEntryAssembly().Location) + "\\users.dat";

        static Timer timer;

        private static List<GiornoRaccolta> Calendario = null;

        public static void Main(string[] args)
        {
            if (!File.Exists(UsersFile))
            {
                List<RegisteredUsers> listUsers = new List<RegisteredUsers>();
                // serialize JSON to a string and then write string to a file
                File.WriteAllText(UsersFile, JsonConvert.SerializeObject(listUsers));
            }

            var me = Bot.GetMeAsync().Result;
            Console.Title = me.Username;

            Bot.OnMessage += BotOnMessageReceived;
            Bot.OnMessageEdited += BotOnMessageReceived;
            Bot.OnCallbackQuery += BotOnCallbackQueryReceived;
            Bot.OnInlineQuery += BotOnInlineQueryReceived;
            Bot.OnInlineResultChosen += BotOnChosenInlineResultReceived;
            Bot.OnReceiveError += BotOnReceiveError;

            schedule_Timer();

            Bot.StartReceiving(Array.Empty<UpdateType>());
            Console.WriteLine($"Start listening for @{me.Username}");
            Console.ReadLine();
            Bot.StopReceiving();
        }

        private static async void BotOnMessageReceived(object sender, MessageEventArgs messageEventArgs)
        {
            List<RegisteredUsers> listUsers = JsonConvert.DeserializeObject<List<RegisteredUsers>>(File.ReadAllText(UsersFile));

            var message = messageEventArgs.Message;

            if (message == null || message.Type != MessageType.Text) return;

            switch (message.Text.Split(' ').First())
            {
                //// send inline keyboard
                //case "/inline":
                //    await Bot.SendChatActionAsync(message.Chat.Id, ChatAction.Typing);

                //    await Task.Delay(500); // simulate longer running task

                //    var inlineKeyboard = new InlineKeyboardMarkup(new[]
                //    {
                //        new [] // first row
                //        {
                //            InlineKeyboardButton.WithCallbackData("1.1"),
                //            InlineKeyboardButton.WithCallbackData("1.2"),
                //        },
                //        new [] // second row
                //        {
                //            InlineKeyboardButton.WithCallbackData("2.1"),
                //            InlineKeyboardButton.WithCallbackData("2.2"),
                //        }
                //    });

                //    await Bot.SendTextMessageAsync(
                //        message.Chat.Id,
                //        "Choose",
                //        replyMarkup: inlineKeyboard);
                //    break;

                //// send custom keyboard
                //case "/keyboard":
                //    ReplyKeyboardMarkup ReplyKeyboard = new[]
                //    {
                //        new[] { "1.1", "1.2" },
                //        new[] { "2.1", "2.2" },
                //    };

                //    await Bot.SendTextMessageAsync(
                //        message.Chat.Id,
                //        "Choose",
                //        replyMarkup: ReplyKeyboard);
                //    break;

                //// send a photo
                //case "/photo":
                //    await Bot.SendChatActionAsync(message.Chat.Id, ChatAction.UploadPhoto);

                //    const string file = @"Files/tux.png";

                //    var fileName = file.Split(Path.DirectorySeparatorChar).Last();

                //    using (var fileStream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read))
                //    {
                //        await Bot.SendPhotoAsync(
                //            message.Chat.Id,
                //            fileStream,
                //            "Nice Picture");
                //    }
                //    break;

                //// request location or contact
                //case "/request":
                //    var RequestReplyKeyboard = new ReplyKeyboardMarkup(new[]
                //    {
                //        KeyboardButton.WithRequestLocation("Location"),
                //        KeyboardButton.WithRequestContact("Contact"),
                //    });

                //    await Bot.SendTextMessageAsync(
                //        message.Chat.Id,
                //        "Who or Where are you?",
                //        replyMarkup: RequestReplyKeyboard);
                //    break;

                case "/register":

                    string msg = @"Utente gia' registrato";

                    if (listUsers.Where(x=> x.ChatID == message.Chat.Id.ToString()).FirstOrDefault() == null)
                    {
                        listUsers.Add(new RegisteredUsers() { ChatID = message.Chat.Id.ToString() });
                        msg = @"Utente registrato con successo";
                        File.WriteAllText(UsersFile, JsonConvert.SerializeObject(listUsers));
                    }

                    await Bot.SendTextMessageAsync(
                        message.Chat.Id,
                        msg,
                        replyMarkup: new ReplyKeyboardRemove());
                    break;

                case "/unregister":

                    msg = @"Utente non presente";

                    if (listUsers.Where(x => x.ChatID == message.Chat.Id.ToString()).FirstOrDefault() != null)
                    {
                        listUsers.RemoveAll(x => x.ChatID == message.Chat.Id.ToString());
                        msg = @"Utente rimosso dal sistema";
                        File.WriteAllText(UsersFile, JsonConvert.SerializeObject(listUsers));
                    }

                    await Bot.SendTextMessageAsync(
                        message.Chat.Id,
                        msg,
                        replyMarkup: new ReplyKeyboardRemove());
                    break;

                case "/domani":

                    await Bot.SendTextMessageAsync(
                        message.Chat.Id,
                        MsgRaccoltaTraXGiorni(1),
                        replyMarkup: new ReplyKeyboardRemove());

                    break;

                case "/tra":

                    var args = message.Text.Split(' ');
                    int days = 0;

                    string res = "Uso: /tra X";

                    if (args.Length == 2 && Int32.TryParse(args[1], out days))
                        res = MsgRaccoltaTraXGiorni(days);

                    await Bot.SendTextMessageAsync(
                        message.Chat.Id,
                        res,
                        replyMarkup: new ReplyKeyboardRemove());

                    break;

                case "/prossime":

                    var culture = new System.Globalization.CultureInfo("it-IT");

                    string nxt = "";

                    for (int i = 1; i < 8; i++)
                    {
                        GiornoRaccolta gr = GetRaccolta(i);
                        if (gr.Raccolta.Count > 0)
                            nxt += string.Format("{0} raccolta {1}",
                                culture.DateTimeFormat.GetDayName(DateTime.Today.AddDays(i).DayOfWeek), string.Join(",", gr.Raccolta.ToArray())) + Environment.NewLine;
                        else
                            nxt += string.Format("{0}",
                                culture.DateTimeFormat.GetDayName(DateTime.Today.AddDays(i).DayOfWeek)) + Environment.NewLine;
                    }

                    await Bot.SendTextMessageAsync(
                        message.Chat.Id,
                        nxt,
                        replyMarkup: new ReplyKeyboardRemove());

                    break;

                default:
                    const string usage = @"
Usage:
/register   - Ricevi notifiche
/unregister - Silenzia notifiche
/domani - Raccolta di domani
/prossime - Prossime raccolte";

                    await Bot.SendTextMessageAsync(
                        message.Chat.Id,
                        usage,
                        replyMarkup: new ReplyKeyboardRemove());
                    break;
            }
        }

        private static async void BotOnCallbackQueryReceived(object sender, CallbackQueryEventArgs callbackQueryEventArgs)
        {
            var callbackQuery = callbackQueryEventArgs.CallbackQuery;

            await Bot.AnswerCallbackQueryAsync(
                callbackQuery.Id,
                $"Received {callbackQuery.Data}");

            await Bot.SendTextMessageAsync(
                callbackQuery.Message.Chat.Id,
                $"Received {callbackQuery.Data}");
        }

        private static async void BotOnInlineQueryReceived(object sender, InlineQueryEventArgs inlineQueryEventArgs)
        {
            Console.WriteLine($"Received inline query from: {inlineQueryEventArgs.InlineQuery.From.Id}");

            InlineQueryResultBase[] results = {
                new InlineQueryResultLocation(
                    id: "1",
                    latitude: 40.7058316f,
                    longitude: -74.2581888f,
                    title: "New York")   // displayed result
                    {
                        InputMessageContent = new InputLocationMessageContent(
                            latitude: 40.7058316f,
                            longitude: -74.2581888f)    // message if result is selected
                    },

                new InlineQueryResultLocation(
                    id: "2",
                    latitude: 13.1449577f,
                    longitude: 52.507629f,
                    title: "Berlin") // displayed result
                    {

                        InputMessageContent = new InputLocationMessageContent(
                            latitude: 13.1449577f,
                            longitude: 52.507629f)   // message if result is selected
                    }
            };

            await Bot.AnswerInlineQueryAsync(
                inlineQueryEventArgs.InlineQuery.Id,
                results,
                isPersonal: true,
                cacheTime: 0);
        }

        private static void BotOnChosenInlineResultReceived(object sender, ChosenInlineResultEventArgs chosenInlineResultEventArgs)
        {
            Console.WriteLine($"Received inline result: {chosenInlineResultEventArgs.ChosenInlineResult.ResultId}");
        }

        private static void BotOnReceiveError(object sender, ReceiveErrorEventArgs receiveErrorEventArgs)
        {
            Console.WriteLine("Received error: {0} — {1}",
                receiveErrorEventArgs.ApiRequestException.ErrorCode,
                receiveErrorEventArgs.ApiRequestException.Message);
        }

        private static void schedule_Timer()
        {
            Console.WriteLine("### Timer Started ###");

            DateTime nowTime = DateTime.Now;
            DateTime scheduledTime = new DateTime(nowTime.Year, nowTime.Month, nowTime.Day, 19, 30, 0, 0); //Specify your scheduled time HH,MM,SS
            if (nowTime > scheduledTime)
            {
                scheduledTime = scheduledTime.AddDays(1);
            }

            double tickTime = (double)(scheduledTime - DateTime.Now).TotalMilliseconds;
            timer = new Timer(tickTime);
            timer.Elapsed += new ElapsedEventHandler(sendMessages);
            timer.Start();
        }

        private static async void sendMessages(object sender, ElapsedEventArgs e)
        {
            Console.WriteLine("### Timer Stopped ### \n");
            timer.Stop();
            string resp = MsgRaccoltaTraXGiorni(1);

            List<RegisteredUsers> listUsers = JsonConvert.DeserializeObject<List<RegisteredUsers>>(File.ReadAllText(UsersFile));
            foreach (RegisteredUsers u in listUsers)
            {
                await Bot.SendTextMessageAsync(
                    u.ChatID,
                    resp,
                    replyMarkup: new ReplyKeyboardRemove());
            }

            schedule_Timer();
        }

        private static string MsgRaccoltaTraXGiorni(int days)
        {
            GiornoRaccolta tomorrow = GetRaccolta(days);

            string resp = @"Nessuna info per domani";

            if (tomorrow != null)
            {
                if (tomorrow.Raccolta.Count == 0)
                    resp = @"Nessuna raccolta domani";
                else
                {
                    if (days == 1)
                        resp = @"Domani raccolta " + string.Join(",", tomorrow.Raccolta.ToArray());
                    else
                        resp = string.Format(@"Tra {0} giorni, raccolta {1}", days, string.Join(",", tomorrow.Raccolta.ToArray()));
                }
            }

            return resp;
        }

        private static GiornoRaccolta GetRaccolta(int days)
        {
            if (Calendario == null)
                Calendario = ReadFromCsv();

            GiornoRaccolta raccolta = Calendario.Where(x => x.Giorno == DateTime.Today.AddDays(days)).FirstOrDefault();

            return raccolta;
        }

        private static List<GiornoRaccolta> ReadFromCsv()
        {
            List<GiornoRaccolta> res = new List<GiornoRaccolta>();

            using (var reader = new StreamReader(@"calendario_raccolta.csv"))
            {
                while (!reader.EndOfStream)
                {
                    var line = reader.ReadLine();
                    GiornoRaccolta gr = new GiornoRaccolta();

                    var values = line.Split(',');

                    gr.Giorno = DateTime.Parse(values[0]);

                    for (int i = 1; i < values.Length; i++)
                    {
                        gr.AggiungiRaccolta(values[i]);
                    }

                    res.Add(gr);
                }
            }

            return res;
        }
    }

    public class RegisteredUsers
    {
        public string ChatID { get; set; }
    }

    public class GiornoRaccolta
    {
        public DateTime Giorno { get; set; }
        public List<TipoRaccolta> Raccolta { get; set; }

        public void AggiungiRaccolta(string cod)
        {
            if (Raccolta == null)
                Raccolta = new List<TipoRaccolta>();

            switch (cod)
            {
                case "ca":
                    Raccolta.Add(TipoRaccolta.Carta);
                    break;
                case "pl":
                    Raccolta.Add(TipoRaccolta.Plastica);
                    break;
                case "or":
                    Raccolta.Add(TipoRaccolta.Organico);
                    break;
                case "ve":
                    Raccolta.Add(TipoRaccolta.Vetro);
                    break;
                case "in":
                    Raccolta.Add(TipoRaccolta.Indifferenziato);
                    break;
            }
        }
    }

    public enum TipoRaccolta
    {
        Carta,
        Plastica,
        Vetro,
        Organico,
        Indifferenziato
    }
}
