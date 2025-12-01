using System;
using System.Collections.Generic;

namespace PoolMate.Api.Common
{
    public class ValidationException : Exception
    {
        public ValidationException(string message) : base(message)
        {
            Errors = new List<string>();
        }

        public ValidationException(string message, IEnumerable<string> errors) : base(message)
        {
            Errors = new List<string>(errors ?? Array.Empty<string>());
        }

        public IList<string> Errors { get; }
    }
}
