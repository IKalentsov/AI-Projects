namespace QwenAnswers.Chat;

using Microsoft.Extensions.AI;

/// <summary>
/// Сессия чата: отправка запросов, обработка отмены и таймаута, ведение истории.
/// </summary>
public class ChatSession : IChatSession
{
    private readonly IChatClient _chatClient;
    private readonly ChatHistoryManager _historyManager;
    private readonly int _requestTimeoutSeconds;

    /// <summary>
    /// Создаёт сессию чата.
    /// </summary>
    /// <param name="chatClient">Клиент для общения с моделью.</param>
    /// <param name="historyManager">Управляет историей чата.</param>
    /// <param name="requestTimeoutSeconds">Таймаут запроса в секундах (по умолчанию 300).</param>
    /// <exception cref="ArgumentOutOfRangeException">Если <paramref name="requestTimeoutSeconds"/> &lt;= 0.</exception>
    public ChatSession(IChatClient chatClient, ChatHistoryManager historyManager, int requestTimeoutSeconds = 300)
    {
        if (requestTimeoutSeconds <= 0)
            throw new ArgumentOutOfRangeException(nameof(requestTimeoutSeconds), requestTimeoutSeconds, "Значение должно быть больше нуля.");

        _chatClient = chatClient ?? throw new ArgumentNullException(nameof(chatClient));
        _historyManager = historyManager ?? throw new ArgumentNullException(nameof(historyManager));
        _requestTimeoutSeconds = requestTimeoutSeconds;
    }

    /// <inheritdoc />
    public async Task<ChatResult> SendAsync(string userMessage, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userMessage);

        // Токен с таймаутом, объединённый с токеном отмены (Esc).
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(_requestTimeoutSeconds));

        try
        {
            // Текущий вопрос передаётся в запрос, но в историю попадает только вместе с ответом —
            // поэтому неудачный запрос не оставляет в истории «вопрос без ответа».
            var messages = _historyManager.BuildRequestMessages(userMessage);
            var response = await _chatClient.GetResponseAsync(messages, cancellationToken: cts.Token);

            var assistantReply = response.Text;

            _historyManager.AddPair(
                new ChatMessage(ChatRole.User, userMessage),
                new ChatMessage(ChatRole.Assistant, assistantReply));

            return new ChatResult(ChatResultType.Success, assistantReply);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Отмена по Esc
            return new ChatResult(ChatResultType.Cancelled, null);
        }
        catch (OperationCanceledException)
        {
            // Сработал таймаут запроса
            return new ChatResult(ChatResultType.Timeout, null);
        }
        catch (Exception ex)
        {
            return new ChatResult(ChatResultType.Error, ex.Message);
        }
    }
}
