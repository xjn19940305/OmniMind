using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
using OmniMind.Abstractions.Storage;
using OmniMind.Entities;
using OmniMind.Enums;
using OmniMind.Messaging.Abstractions;
using OmniMind.Persistence.PostgreSql;

namespace OmniMind.Messaging.RabbitMQ.Consumers
{
    /// <summary>
    /// Handles completed transcription messages from the external ASR service.
    /// </summary>
    public class TranscribeCompletedConsumer : RabbitMQMessageConsumer
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly MessageRetryPolicy retryPolicy = new();

        public TranscribeCompletedConsumer(
            IOptions<RabbitMQOptions> options,
            IServiceProvider serviceProvider)
            : base(options, options.Value.TranscribeCompletedQueue)
        {
            _serviceProvider = serviceProvider;
        }

        public void StartConsuming(
            Func<int> getInFlight,
            Action<int> setInFlight,
            CancellationToken stoppingToken)
        {
            StartConsuming<Messages.TranscribeCompletedMessage>(
                HandleTranscribeCompletedAsync,
                getInFlight,
                setInFlight,
                stoppingToken);
        }

        private async Task HandleTranscribeCompletedAsync(
            Messages.TranscribeCompletedMessage message,
            CancellationToken token)
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<OmniMindDbContext>();
            var logger = scope.ServiceProvider.GetService<ILogger<TranscribeCompletedConsumer>>();

            var document = await dbContext.Documents
                .FirstOrDefaultAsync(x => x.Id == message.DocumentId, token);

            if (document == null)
            {
                logger?.LogWarning(
                    "[TranscribeCompleted] Document not found. DocumentId={DocumentId}",
                    message.DocumentId);
                return;
            }

            logger?.LogInformation(
                "[TranscribeCompleted] Start handling callback. DocumentId={DocumentId}, Status={Status}",
                message.DocumentId,
                message.Status);

            if (message.Status == Messages.TranscribeStatus.Failed ||
                message.Status == Messages.TranscribeStatus.Timeout)
            {
                await dbContext.Documents
                    .Where(x => x.Id == document.Id)
                    .ExecuteUpdateAsync(d => d
                        .SetProperty(x => x.Status, DocumentStatus.Failed)
                        .SetProperty(x => x.Error, message.Error ?? "Transcription failed")
                        .SetProperty(x => x.UpdatedAt, DateTimeOffset.UtcNow), token);

                logger?.LogError(
                    "[TranscribeCompleted] Transcription failed. DocumentId={DocumentId}, Error={Error}",
                    message.DocumentId,
                    message.Error);
                return;
            }

            var objectStorage = scope.ServiceProvider.GetRequiredService<IObjectStorage>();
            string rawTranscriptionPayload;
            string transcribedText;

            try
            {
                logger?.LogInformation(
                    "[TranscribeCompleted] Downloading transcription payload. ObjectKey={ObjectKey}",
                    message.TranscribedTextObjectKey);

                using var stream = await objectStorage.GetAsync(message.TranscribedTextObjectKey, token);
                using var reader = new StreamReader(stream);
                rawTranscriptionPayload = await reader.ReadToEndAsync(token);

                if (string.IsNullOrWhiteSpace(rawTranscriptionPayload))
                {
                    throw new InvalidOperationException("Transcription payload is empty");
                }

                transcribedText = TryExtractTranscriptionText(rawTranscriptionPayload, out var extractedText)
                    ? extractedText
                    : rawTranscriptionPayload;

                logger?.LogInformation(
                    "[TranscribeCompleted] Transcription payload downloaded. TextLength={TextLength}",
                    transcribedText.Length);
            }
            catch (Exception ex)
            {
                logger?.LogError(
                    ex,
                    "[TranscribeCompleted] Failed to download transcription payload. DocumentId={DocumentId}",
                    message.DocumentId);

                await dbContext.Documents
                    .Where(x => x.Id == document.Id)
                    .ExecuteUpdateAsync(d => d
                        .SetProperty(x => x.Status, DocumentStatus.Failed)
                        .SetProperty(x => x.Error, $"Failed to download transcription payload: {ex.Message}")
                        .SetProperty(x => x.UpdatedAt, DateTimeOffset.UtcNow), token);
                return;
            }

            document.Content = transcribedText;
            document.Transcription = rawTranscriptionPayload;
            document.Status = DocumentStatus.Parsed;
            document.UpdatedAt = DateTimeOffset.UtcNow;
            await dbContext.SaveChangesAsync(token);

            var result = await retryPolicy.ExecuteAsync(
                documentId: document.Id,
                currentRetryCount: document.RetryCount,
                processAction: () => ProcessWithRetryAsync(scope, document, dbContext, logger, token),
                republishAction: () => RepublishMessageAsync(message, scope, logger, token),
                logger: logger,
                cancellationToken: token);

            await HandleResultAsync(result, document, dbContext, logger, token);
        }

        private async Task ProcessWithRetryAsync(
            IServiceScope scope,
            Document document,
            OmniMindDbContext dbContext,
            ILogger? logger,
            CancellationToken token)
        {
            document.RetryCount++;
            document.LastRetryAt = DateTimeOffset.UtcNow;
            await dbContext.SaveChangesAsync(token);

            await DocumentProcessor.ProcessDocumentAsync(scope, document, dbContext, logger);

            await dbContext.Documents
                .Where(x => x.Id == document.Id)
                .ExecuteUpdateAsync(d => d
                    .SetProperty(x => x.RetryCount, 0)
                    .SetProperty(x => x.LastRetryAt, (DateTimeOffset?)null), token);
        }

        private async Task RepublishMessageAsync(
            Messages.TranscribeCompletedMessage originalMessage,
            IServiceScope scope,
            ILogger? logger,
            CancellationToken token)
        {
            var messagePublisher = scope.ServiceProvider.GetService<IMessagePublisher>();
            if (messagePublisher == null)
            {
                logger?.LogWarning(
                    "[TranscribeCompleted] IMessagePublisher not found. DocumentId={DocumentId}",
                    originalMessage.DocumentId);
                return;
            }

            await messagePublisher.PublishTranscribeCompletedAsync(originalMessage, token);

            logger?.LogInformation(
                "[TranscribeCompleted] Message requeued. DocumentId={DocumentId}",
                originalMessage.DocumentId);
        }

        private async Task HandleResultAsync(
            RetryResult result,
            Document document,
            OmniMindDbContext dbContext,
            ILogger? logger,
            CancellationToken token)
        {
            if (result == RetryResult.Failed || result == RetryResult.MaxRetriesExceeded)
            {
                var errorMsg = result == RetryResult.MaxRetriesExceeded
                    ? $"Post-transcription processing failed after {retryPolicy.MaxRetryCount} retries"
                    : "Post-transcription processing failed";

                await dbContext.Documents
                    .Where(x => x.Id == document.Id)
                    .ExecuteUpdateAsync(d => d
                        .SetProperty(x => x.Status, DocumentStatus.Failed)
                        .SetProperty(x => x.Error, errorMsg)
                        .SetProperty(x => x.UpdatedAt, DateTimeOffset.UtcNow), token);

                logger?.LogError(
                    "[TranscribeCompleted] Processing failed. DocumentId={DocumentId}",
                    document.Id);
            }
        }

        private static bool TryExtractTranscriptionText(
            string payload,
            out string transcriptionText)
        {
            transcriptionText = string.Empty;

            try
            {
                var token = JToken.Parse(payload);
                if (token.Type != JTokenType.Object)
                {
                    return false;
                }

                transcriptionText =
                    token.Value<string>("fullText")
                    ?? token.Value<string>("text")
                    ?? token.Value<string>("Text")
                    ?? token["data"]?["text"]?.Value<string>()
                    ?? string.Empty;

                return !string.IsNullOrWhiteSpace(transcriptionText);
            }
            catch
            {
                return false;
            }
        }
    }
}
