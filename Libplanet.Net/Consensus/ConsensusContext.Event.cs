using System;
using Libplanet.Action;
using Libplanet.Net.Messages;

namespace Libplanet.Net.Consensus
{
    public partial class ConsensusContext<T>
        where T : IAction, new()
    {
        /// <inheritdoc cref="Context{T}.ExceptionOccurred"/>
        internal event EventHandler<(long Height, Exception)>? ExceptionOccurred;

        /// <inheritdoc cref="Context{T}.TimeoutProcessed"/>
        internal event EventHandler<(long Height, int Round)>? TimeoutProcessed;

        /// <inheritdoc cref="Context{T}.StateChanged"/>
        internal event EventHandler<
                (long Height, int MessageLogSize, int Round, Step Step)>?
            StateChanged;

        /// <inheritdoc cref="Context{T}.MessageConsumed"/>
        internal event EventHandler<(long Height, ConsensusMsg Message)>?
            MessageConsumed;

        /// <inheritdoc cref="Context{T}.MutationConsumed"/>
        internal event EventHandler<(long Height, System.Action)>? MutationConsumed;

        private void AttachEventHandlers(Context<T> context)
        {
            context.ExceptionOccurred += (sender, exception) =>
                ExceptionOccurred?.Invoke(this, exception);

            context.TimeoutProcessed += (sender, timeoutStart) =>
                TimeoutProcessed?.Invoke(this, timeoutStart);

            context.StateChanged += (sender, state) =>
                StateChanged?.Invoke(this, state);

            context.MessageConsumed += (sender, message) =>
                MessageConsumed?.Invoke(this, message);

            context.MutationConsumed += (sender, action) =>
                MutationConsumed?.Invoke(this, action);
        }
    }
}
