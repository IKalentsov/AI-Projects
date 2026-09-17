namespace QwenAnswers.Chat;

using System.ClientModel;
using Microsoft.Extensions.AI;
using OpenAI;
using QwenAnswers.Config;

/// <summary>
/// Собирает боевые компоненты чата из конфигурации.
/// Вынесено из Program.cs, чтобы тесты проверяли ровно тот же путь сборки, что и приложение.
/// </summary>
public static class ChatSessionFactory
{
    /// <summary>System-подсказка, с которой начинается каждый запрос к модели.</summary>
    public const string SystemPrompt = "Ты полезный ассистент. Отвечай кратко и по делу.";

    /// <summary>
    /// Создаёт клиент модели для OpenAI-совместимого сервера.
    /// </summary>
    /// <param name="config">Проверенная конфигурация из .env.</param>
    public static IChatClient CreateChatClient(AppConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        var options = new OpenAIClientOptions { Endpoint = new Uri(config.Endpoint, UriKind.Absolute) };
        var openAiClient = new OpenAIClient(new ApiKeyCredential(config.ApiKey), options);

        return openAiClient.GetChatClient(config.ModelName).AsIChatClient();
    }

    /// <summary>
    /// Создаёт историю чата с system-подсказкой и лимитом из конфигурации.
    /// </summary>
    public static ChatHistoryManager CreateHistory(AppConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        return new ChatHistoryManager(SystemPrompt, config.MaxHistoryMessages);
    }

    /// <summary>
    /// Создаёт сессию чата с историей и таймаутом из конфигурации.
    /// </summary>
    public static ChatSession CreateSession(IChatClient chatClient, AppConfig config)
    {
        ArgumentNullException.ThrowIfNull(chatClient);
        ArgumentNullException.ThrowIfNull(config);

        return new ChatSession(chatClient, CreateHistory(config), config.RequestTimeoutSeconds);
    }
}
