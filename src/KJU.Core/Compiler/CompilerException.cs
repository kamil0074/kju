namespace KJU.Core.Compiler
{
    using System;

    public class CompilerException : Exception
    {
        public CompilerException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}