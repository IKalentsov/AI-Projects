namespace QwenAnswers.Chat;

using Microsoft.Extensions.AI;

/// <summary>
/// История чата с ограничением на количество хранимых реплик.
/// System-сообщение хранится отдельно и в лимит не входит.
/// Реплики удаляются целиком (user + assistant), чтобы в истории не оставалось вопроса без ответа.
/// История меняется только после успешного ответа модели: неудавшийся запрос в неё не попадает.
/// </summary>
public class ChatHistoryManager
{
    private readonly string _systemMessage;
    private readonly int _maxMessages;
    private readonly List<ChatMessage> _history = new();

    /// <summary>
    /// Создаёт менеджер истории чата.
    /// </summary>
    /// <param name="systemMessage">System-сообщение, всегда добавляется в начало запроса.</param>
    /// <param name="maxMessages">
    /// Максимальное количество реплик диалога (пар «вопрос + ответ») в истории. По умолчанию 50.
    /// Считаются только сообщения пользователя, System-сообщение не учитывается.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">Если <paramref name="maxMessages"/> &lt;= 0.</exception>
    /// <exception cref="ArgumentNullException">Если <paramref name="systemMessage"/> равен null.</exception>
    public ChatHistoryManager(string systemMessage, int maxMessages = 50)
    {
        if (maxMessages <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxMessages), maxMessages, "Значение должно быть больше нуля.");

        _systemMessage = systemMessage ?? throw new ArgumentNullException(nameof(systemMessage));
        _maxMessages = maxMessages;
    }

    /// <summary>
    /// Количество реплик пользователя в истории.
    /// </summary>
    public int UserMessageCount => _history.Count(m => m.Role == ChatRole.User);

    /// <summary>
    /// Добавляет завершённую реплику: вопрос пользователя и ответ ассистента.
    /// После добавления лимит истории применяется автоматически.
    /// </summary>
    public void AddPair(ChatMessage userMessage, ChatMessage assistantMessage)
    {
        ArgumentNullException.ThrowIfNull(userMessage);
        ArgumentNullException.ThrowIfNull(assistantMessage);

        _history.Add(userMessage);
        _history.Add(assistantMessage);

        EnforceLimit();
    }

    /// <summary>
    /// Формирует сообщения для запроса к модели: System + история + (опционально) вопрос текущего запроса.
    /// Возвращает снимок: последующие изменения истории на уже полученный результат не влияют.
    /// </summary>
    /// <param name="pendingUserMessage">
    /// Вопрос, которого ещё нет в истории (текущий запрос). Если null или пусто — не добавляется.
    /// </param>
    public IReadOnlyList<ChatMessage> BuildRequestMessages(string? pendingUserMessage = null)
    {
        var messages = new List<ChatMessage>(_history.Count + 2)
        {
            new(ChatRole.System, _systemMessage),
        };

        messages.AddRange(_history);

        if (!string.IsNullOrEmpty(pendingUserMessage))
            messages.Add(new ChatMessage(ChatRole.User, pendingUserMessage));

        return messages;
    }

    private void EnforceLimit()
    {
        // userCount > _maxMessages >= 1 означает, что в истории минимум две реплики пользователя,
        // поэтому удаление первых двух элементов всегда безопасно.
        while (UserMessageCount > _maxMessages)
        {
            _history.RemoveAt(0);
            _history.RemoveAt(0);
        }
    }
}
