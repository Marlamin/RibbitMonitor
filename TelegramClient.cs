using Microsoft.Extensions.Configuration;
using System.IO;

namespace RibbitMonitor
{
    public class TelegramClient
    {
        public static void SendMessage(string message)
        {
            var config = new ConfigurationBuilder().SetBasePath(Directory.GetCurrentDirectory()).AddJsonFile("settings.json", true, true).Build();
            if (string.IsNullOrEmpty(config["telegramToken"]))
            {
                return;
            }

            new Telegram.Bot.TelegramBotClient(config["telegramToken"]).SendTextMessageAsync(-252446413, message);
        }
    }
}
