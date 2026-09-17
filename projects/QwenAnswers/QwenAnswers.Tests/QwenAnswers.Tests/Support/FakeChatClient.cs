namespace QwenAnswers.Tests.Support;

using Microsoft.Extensions.AI;
using Moq;

/// <summary>
/// Готовые подмены <see cref="IChatClient"/> для тестов, которым не нужна реальная модель.
/// </summary>
internal static class FakeChatClient
{
    /// <summary>
    /// Клиент, который сразу отвечает указанным текстом.
    /// </summary>
    /// <param name="reply">Текст ответа ассистента.</param>
    /// <param name="capturedRequests">
    /// Если передан, в него складываются сообщения каждого запроса — удобно проверять,
    /// что именно уходит модели.
    /// </param>
    public static Mock<IChatClient> Replying(string reply, List<List<ChatMessage>>? capturedRequests = null)
    {
        var client = new Mock<IChatClient>();

        client
            .Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .Callback((IEnumerable<ChatMessage> messages, ChatOptions? options, CancellationToken cancellationToken) =>
                capturedRequests?.Add(messages.ToList()))
            .ReturnsAsync(new ChatResponse(new ChatMessage(ChatRole.Assistant, reply)));

        return client;
    }

    /// <summary>
    /// Клиент, который «думает» бесконечно и завершается только по отмене токена.
    /// Позволяет детерминированно проверить таймаут и отмену по Esc.
    /// </summary>
    public static Mock<IChatClient> NeverReplies()
    {
        var client = new Mock<IChatClient>();

        client
            .Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .Returns((IEnumerable<ChatMessage> messages, ChatOptions? options, CancellationToken cancellationToken) =>
                Pending<ChatResponse>(cancellationToken));

        return client;
    }

    /// <summary>Клиент, который падает с указанным исключением.</summary>
    public static Mock<IChatClient> FailingWith(Exception exception)
    {
        var client = new Mock<IChatClient>();

        client
            .Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(exception);

        return client;
    }

    /// <summary>Задача, которая не завершится, пока не отменят токен.</summary>
    public static Task<T> Pending<T>(CancellationToken cancellationToken)
    {
        var source = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);

        cancellationToken.Register(() => source.TrySetCanceled(cancellationToken));

        return source.Task;
    }
}
