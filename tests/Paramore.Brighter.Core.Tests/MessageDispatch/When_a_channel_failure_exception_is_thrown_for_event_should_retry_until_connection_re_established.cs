﻿#region Licence
/* The MIT License (MIT)
Copyright © 2014 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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
using System.Linq;
using FluentAssertions;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Core.Tests.MessageDispatch.TestDoubles;
using Xunit;
using Paramore.Brighter.ServiceActivator;
using System.Text.Json;

namespace Paramore.Brighter.Core.Tests.MessageDispatch
{
    public class MessagePumpRetryEventConnectionFailureTests
    {
        private readonly IAmAMessagePump _messagePump;
        private readonly SpyCommandProcessor _commandProcessor;
        private FailingChannel _channel;
        private int CONN_FAIL_PAUSE;

        public MessagePumpRetryEventConnectionFailureTests()
        {
            _commandProcessor = new SpyCommandProcessor();
            _channel = new FailingChannel { NumberOfRetries = 1 };
            var mapper = new MyEventMessageMapper();
            CONN_FAIL_PAUSE = 2000;
            _messagePump = new MessagePumpBlocking<MyEvent>(_commandProcessor, mapper) { Channel = _channel, TimeoutInMilliseconds = 500, RequeueCount = -1, ConnectionFailureRetryIntervalinMs = CONN_FAIL_PAUSE};

            var @event = new MyEvent();

            //Two events will be received when channel fixed
            var message1 = new Message(new MessageHeader(Guid.NewGuid(), "MyTopic", MessageType.MT_EVENT), new MessageBody(JsonSerializer.Serialize(@event, JsonSerialisationOptions.Options)));
            var message2 = new Message(new MessageHeader(Guid.NewGuid(), "MyTopic", MessageType.MT_EVENT), new MessageBody(JsonSerializer.Serialize(@event, JsonSerialisationOptions.Options)));
            _channel.Enqueue(message1);
            _channel.Enqueue(message2);
            
            //Quit the message pump
            var quitMessage = new Message(new MessageHeader(Guid.Empty, "", MessageType.MT_QUIT), new MessageBody(""));
            _channel.Enqueue(quitMessage);
        }

        [Fact]
        public void When_A_Channel_Failure_Exception_Is_Thrown_For_Event_Should_Retry_Until_Connection_Re_established()
        {
            _messagePump.Run();

            //_should_publish_the_message_via_the_command_processor
            _commandProcessor.Commands.Count().Should().Be(2);
            _commandProcessor.Commands[0].Should().Be(CommandType.Publish);
            _commandProcessor.Commands[1].Should().Be(CommandType.Publish);
            _channel.PauseWaitInMs.Should().Be(CONN_FAIL_PAUSE);
        }

    }
}
