﻿#region Licence

/* The MIT License (MIT)
Copyright © 2015 Toby Henderson <hendersont@gmail.com>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the “Software”), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED “AS IS”, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */

#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles
{
    public class FakeOutboxSync : IAmABulkOutboxSync<Message>, IAmABulkOutboxAsync<Message>
    {
        private readonly List<OutboxEntry> _posts = new List<OutboxEntry>();

        public bool ContinueOnCapturedContext { get; set; }

        public void Add(Message message, int outBoxTimeout = -1, IAmABoxTransactionConnectionProvider transactionConnectionProvider = null)
        {
            _posts.Add(new OutboxEntry {Message = message, TimeDeposited = DateTime.UtcNow});
        }

        public Task AddAsync(Message message, int outBoxTimeout = -1, CancellationToken cancellationToken = default(CancellationToken), IAmABoxTransactionConnectionProvider transactionConnectionProvider = null)
        {
            if (cancellationToken.IsCancellationRequested)
                return Task.FromCanceled(cancellationToken);

            Add(message, outBoxTimeout);

            return Task.FromResult(0);
        }

        public IEnumerable<Message> DispatchedMessages(
            double millisecondsDispatchedSince,
            int pageSize = 100,
            int pageNumber = 1,
            int outboxTimeout = -1,
            Dictionary<string, object> args = null)
        {
            var ago = millisecondsDispatchedSince * -1;
            var now = DateTime.UtcNow;
            var messagesSince = now.AddMilliseconds(ago);
            return _posts.Where(oe => oe.TimeFlushed >= messagesSince).Select(oe => oe.Message).Take(pageSize).ToArray();
        }

        public Message Get(Guid messageId, int outBoxTimeout = -1)
        {
            foreach (var outboxEntry in _posts)
            {
                if (outboxEntry.Message.Id == messageId)
                {
                    return outboxEntry.Message;
                }
            }

            return null;
        }

        public IList<Message> Get(
            int pageSize = 100, 
            int pageNumber = 1, 
            Dictionary<string, object> args = null)
        {
            return _posts.Select(outboxEntry => outboxEntry.Message).Take(pageSize).ToList();
        }

        public Task<Message> GetAsync(Guid messageId, int outBoxTimeout = -1, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (cancellationToken.IsCancellationRequested)
                return Task.FromCanceled<Message>(cancellationToken);

            return Task.FromResult(Get(messageId, outBoxTimeout));
        }

        public Task<IList<Message>> GetAsync(int pageSize = 100, int pageNumber = 1,
            Dictionary<string, object> args = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return Task.FromResult(Get(pageSize, pageNumber, args));
        }

        public Task<IEnumerable<Message>> GetAsync(IEnumerable<Guid> messageIds, int outBoxTimeout = -1,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            var tcs = new TaskCompletionSource<IEnumerable<Message>>();
            tcs.SetResult(_posts.Where(oe => messageIds.Contains(oe.Message.Id))
                .Select(outboxEntry => outboxEntry.Message).ToList());

            return tcs.Task;
        }

        public Task MarkDispatchedAsync(Guid id, DateTime? dispatchedAt = null, Dictionary<string, object> args = null, CancellationToken cancellationToken = default)
        {
            var tcs = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
            
            MarkDispatched(id, dispatchedAt);
            
            tcs.SetResult(new object());

            return tcs.Task;
        }

        public async Task MarkDispatchedAsync(IEnumerable<Guid> ids, DateTime? dispatchedAt = null, Dictionary<string, object> args = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            foreach (var id in ids)
            {
                await MarkDispatchedAsync(id, dispatchedAt, args, cancellationToken);
            }
        }

        public Task<IEnumerable<Message>> DispatchedMessagesAsync(double millisecondsDispatchedSince, int pageSize = 100, int pageNumber = 1,
            int outboxTimeout = -1, Dictionary<string, object> args = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            return Task.FromResult(DispatchedMessages(millisecondsDispatchedSince, pageSize, pageNumber, outboxTimeout,
                args));
        }

        public Task<IEnumerable<Message>> OutstandingMessagesAsync(double millSecondsSinceSent, int pageSize = 100, int pageNumber = 1,
            Dictionary<string, object> args = null, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(OutstandingMessages(millSecondsSinceSent, pageSize, pageNumber, args));
        }

        public Task DeleteAsync(CancellationToken cancellationToken, params Guid[] messageIds)
        {
            Delete(messageIds);
            return Task.CompletedTask;
        }

        public void MarkDispatched(Guid id, DateTime? dispatchedAt = null, Dictionary<string, object> args = null)
        {
           var entry = _posts.SingleOrDefault(oe => oe.Message.Id == id);
           entry.TimeFlushed = dispatchedAt ?? DateTime.UtcNow;

        }

       public IEnumerable<Message> OutstandingMessages(
           double millSecondsSinceSent, 
           int pageSize = 100, 
           int pageNumber = 1,
           Dictionary<string, object> args = null)
        {
            var sentAfter = DateTime.UtcNow.AddMilliseconds(-1 * millSecondsSinceSent);
            return _posts
                .Where(oe => oe.TimeDeposited.Value < sentAfter && oe.TimeFlushed == null)
                .Select(oe => oe.Message)
                .Take(pageSize)
                .ToArray();
        }

       public void Delete(params Guid[] messageIds)
       {
           foreach (Guid messageId in messageIds)
           {
               var message = _posts.First(e => e.Message.Id == messageId);
               _posts.Remove(message);
           }
       }

       class OutboxEntry
        {
            public DateTime? TimeDeposited { get; set; }
            public DateTime? TimeFlushed { get; set; }
            public Message Message { get; set; }
        }

        public void Add(IEnumerable<Message> messages, int outBoxTimeout = -1,
            IAmABoxTransactionConnectionProvider transactionConnectionProvider = null)
        {
            foreach (Message message in messages)
            {
                Add(message,outBoxTimeout, transactionConnectionProvider);
            }
        }

        public async Task AddAsync(IEnumerable<Message> messages, int outBoxTimeout = -1,
            CancellationToken cancellationToken = default(CancellationToken),
            IAmABoxTransactionConnectionProvider transactionConnectionProvider = null)
        {
            foreach (var message in messages)
            {
                await AddAsync(message, outBoxTimeout, cancellationToken, transactionConnectionProvider);
            }
        }
    }
}
