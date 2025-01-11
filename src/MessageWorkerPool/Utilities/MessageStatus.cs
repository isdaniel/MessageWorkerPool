// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace MessageWorkerPool.Utilities
{
    public class MessageCommunicate
    {
        public const string CLOSED_SIGNAL = "quit";
    }

    public enum MessageStatus : short {
        IGNORE_MESSAGE = -1,
        MESSAGE_DONE = 200,
        MESSAGE_DONE_WITH_REPLY = 201
    }
}
