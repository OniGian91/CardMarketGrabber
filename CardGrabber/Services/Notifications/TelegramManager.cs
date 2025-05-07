
using CardGrabber.Configuration;

namespace CardGrabber.Services
{
    public class TelegramManager
    {
        private readonly string _botToken;
        private readonly string _chatId;
        private readonly HttpClient _httpClient;

        public TelegramManager()
        {
            var config = ConfigurationLoader.Load();
            _botToken = config.TelegramBot.BotToken;
            _chatId = config.TelegramBot.ChatId;
            _httpClient = new HttpClient();
        }

        public async Task SendNotification(string message)
        {
            if (string.IsNullOrWhiteSpace(_botToken) || string.IsNullOrWhiteSpace(_chatId))
                throw new InvalidOperationException("Telegram bot configuration is missing.");

            string encodedMessage = Uri.EscapeDataString(message);
            string url = $"https://api.telegram.org/bot{_botToken}/sendMessage?chat_id={_chatId}&text={encodedMessage}";

            var response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();

                throw new Exception($"Failed to send Telegram message: {response.StatusCode} - {error}");
            }
        }
    }
}
