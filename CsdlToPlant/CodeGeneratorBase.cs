namespace CsdlToPlant
{
    using System;
    using System.Collections.Generic;
    using System.Text;

    /// <summary>
    /// Base class for generating well-formatted code.
    /// Borrowed from T4's generated base class.
    /// </summary>
    internal class CodeGeneratorBase
    {
        private bool endsWithNewline;

        /// <summary>
        /// The string builder that generation-time code uses to assemble generated output.
        /// </summary>
        protected StringBuilder GenerationEnvironment { get; set; } = new StringBuilder();

        /// <summary>
        /// The error collection for the generation process.
        /// </summary>
        public IList<GenerationError> Errors { get; } = new List<GenerationError>();

        /// <summary>
        /// A list of the lengths of each indent that were added with PushIndent.
        /// </summary>
        private List<int> IndentLengths { get; } = new List<int>();

        /// <summary>
        /// Gets the current indent to use when adding lines to the output.
        /// </summary>
        public string CurrentIndent { get; private set; } = string.Empty;

        /// <summary>
        /// Write text directly into the generated output.
        /// </summary>
        public void Write(string textToAppend)
        {
            if (string.IsNullOrEmpty(textToAppend))
            {
                return;
            }

            // If we're starting off, or if the previous text ended with a newline,
            // we have to append the current indent first.
            if (this.GenerationEnvironment.Length == 0
                || this.endsWithNewline)
            {
                this.GenerationEnvironment.Append(this.CurrentIndent);
                this.endsWithNewline = false;
            }

            // Check if the current text ends with a newline
            if (textToAppend.EndsWith(global::System.Environment.NewLine,
                global::System.StringComparison.CurrentCulture))
            {
                this.endsWithNewline = true;
            }

            // This is an optimization. If the current indent is "", then we don't have to do any
            // of the more complex stuff further down.
            if (this.CurrentIndent.Length == 0)
            {
                this.GenerationEnvironment.Append(textToAppend);
                return;
            }

            // Everywhere there is a newline in the text, add an indent after it
            textToAppend = textToAppend.Replace(global::System.Environment.NewLine,
                global::System.Environment.NewLine + this.CurrentIndent);
            // If the text ends with a newline, then we should strip off the indent added at the very end
            // because the appropriate indent will be added when the next time Write() is called
            if (this.endsWithNewline)
            {
                this.GenerationEnvironment.Append(textToAppend, 0, textToAppend.Length - this.CurrentIndent.Length);
            }
            else
            {
                this.GenerationEnvironment.Append(textToAppend);
            }
        }

        /// <summary>
        /// Write text directly into the generated output.
        /// </summary>
        public void WriteLine(string textToAppend)
        {
            this.Write(textToAppend);
            this.GenerationEnvironment.AppendLine();
            this.endsWithNewline = true;
        }

        /// <summary>
        /// Write formatted text directly into the generated output.
        /// </summary>
        public void Write(string format, params object[] args)
        {
            this.Write(string.Format(System.Globalization.CultureInfo.CurrentCulture, format, args));
        }

        /// <summary>
        /// Write formatted text directly into the generated output.
        /// </summary>
        public void WriteLine(string format, params object[] args)
        {
            this.WriteLine(string.Format(System.Globalization.CultureInfo.CurrentCulture, format, args));
        }

        /// <summary>
        /// Raise an error.
        /// </summary>
        public void Error(string message)
        {
            var error = new GenerationError {ErrorText = message};
            this.Errors.Add(error);
        }

        /// <summary>
        /// Raise a warning.
        /// </summary>
        public void Warning(string message)
        {
            var error = new GenerationError
            {
                ErrorText = message,
                IsWarning = true
            };
            this.Errors.Add(error);
        }

        /// <summary>
        /// Increase the indent.
        /// </summary>
        public void PushIndent(string indent)
        {
            this.CurrentIndent += indent ?? throw new ArgumentNullException(nameof(indent));
            this.IndentLengths.Add(indent.Length);
        }

        /// <summary>
        /// Remove the last indent that was added with PushIndent.
        /// </summary>
        public string PopIndent()
        {
            string returnValue = string.Empty;
            if (this.IndentLengths.Count > 0)
            {
                int indentLength = this.IndentLengths[^1];
                this.IndentLengths.RemoveAt(this.IndentLengths.Count - 1);
                if (indentLength > 0)
                {
                    returnValue = this.CurrentIndent[^indentLength..];
                    this.CurrentIndent = this.CurrentIndent.Remove(this.CurrentIndent.Length - indentLength);
                }
            }

            return returnValue;
        }

        /// <summary>
        /// Remove all indentation.
        /// </summary>
        public void ClearIndent()
        {
            this.IndentLengths.Clear();
            this.CurrentIndent = string.Empty;
        }
    }
}
