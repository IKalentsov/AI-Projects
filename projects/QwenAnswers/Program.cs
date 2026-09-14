using System.ClientModel;
using Microsoft.Extensions.AI;
using OpenAI;

// ── Загрузка настроек из .env ────────────────────────────────────────────────
var envPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ".env");
if (!File.Exists(envPath))
{
    Console.WriteLine("Ошибка: файл .env не найден.");
    Console.WriteLine("Скопируйте .env-public в .env и заполните своими данными.");
    return;
}

var envLines = File.ReadAllLines(envPath);
var config = new Dictionary<string, string?>();
foreach (var line in envLines)
{
    var trimmed = line.Trim();
    if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith('#'))
        continue;

    var separatorIndex = trimmed.IndexOf('=');
    if (separatorIndex <= 0)
        continue;

    var key = trimmed[..separatorIndex].Trim();
    var value = trimmed[(separatorIndex + 1)..].Trim().Trim('"').Trim('\'');
    config[key] = value;
}

var apiKeyStr = config["AI_API_KEY"] ?? string.Empty;
var endpointStr = config["AI_ENDPOINT"] ?? string.Empty;
var modelName = config["AI_MODEL_NAME"] ?? string.Empty;

if (
    string.IsNullOrEmpty(apiKeyStr)
    || string.IsNullOrEmpty(endpointStr)
    || string.IsNullOrEmpty(modelName)
)
{
    Console.WriteLine(
        "Ошибка: в .env не заполнены все поля (AI_API_KEY, AI_ENDPOINT, AI_MODEL_NAME)."
    );
    return;
}

try
{
    // ── Настройки подключения ───────────────────────────────────────────────────
    var endpoint = new Uri(endpointStr);

    var options = new OpenAIClientOptions { Endpoint = endpoint };
    var openAiClient = new OpenAIClient(new ApiKeyCredential(apiKeyStr), options);

    // Преобразуем в IChatClient через AsIChatClient()
    using var chatClient = openAiClient.GetChatClient(modelName).AsIChatClient();

    // ── История диалога ───────────────────────────────────────────────────
    var chatHistory = new List<ChatMessage>
    {
        new ChatMessage(ChatRole.System, "Ты полезный ассистент. Отвечай кратко и по делу."),
    };

    Console.WriteLine($"Чат с моделью {modelName}. Введите 'exit' для выхода.\n");

    // ── Цикл чтения из консоли ────────────────────────────────────────────
    while (true)
    {
        Console.Write("Вы: ");
        var userInput = Console.ReadLine();

        if (string.IsNullOrWhiteSpace(userInput))
            continue;

        if (
            userInput.Equals("exit", StringComparison.OrdinalIgnoreCase)
            || userInput.Equals("quit", StringComparison.OrdinalIgnoreCase)
        )
            break;

        // Добавляем сообщение пользователя в историю
        chatHistory.Add(new ChatMessage(ChatRole.User, userInput));

        try
        {
            // Отправляем всю историю, чтобы модель помнила контекст
            var response = await chatClient.GetResponseAsync(chatHistory);
            var assistantReply = response.Text;

            Console.WriteLine($"AI: {assistantReply}\n");

            // Сохраняем ответ ассистента в историю
            chatHistory.Add(new ChatMessage(ChatRole.Assistant, assistantReply));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка: {ex.Message}\n");
            // Удаляем последнее сообщение пользователя, чтобы не засорять историю при ошибке
            chatHistory.RemoveAt(chatHistory.Count - 1);
        }
    }
}
catch (Exception ex)
{
    Console.WriteLine($"Критическая ошибка: {ex.Message}");
    Console.WriteLine("Нажмите любую клавишу для выхода...");
    Console.ReadKey();
}
