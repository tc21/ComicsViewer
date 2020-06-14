using System;
using System.Collections.Generic;
using System.Text;

namespace ComicsLibrary.SQL {
    [Serializable]
    public class ComicsDatabaseException : Exception {
        public ComicsDatabaseException() { }
        public ComicsDatabaseException(string message) : base(message) { }
        public ComicsDatabaseException(string message, Exception inner) : base(message, inner) { }
        protected ComicsDatabaseException(
          System.Runtime.Serialization.SerializationInfo info,
          System.Runtime.Serialization.StreamingContext context) : base(info, context) { }
    }
}
