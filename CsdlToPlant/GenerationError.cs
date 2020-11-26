namespace CsdlToPlant
{
    /// <summary>
    /// POCO for errors during generation.
    /// </summary>
    public class GenerationError
    {
        private const string Warning = nameof(Warning);
        private const string Error = nameof(Error);

        /// <summary>Initializes a new instance of the <see cref="T:GenerationError" /> class.</summary>
        public GenerationError()
        {
        }

        /// <summary>Initializes a new instance of the <see cref="T:GenerationError" /> class using the specified file name, line, column, error number, and error text.</summary>
        /// <param name="fileName">The file name of the file that the compiler was compiling when it encountered the error.</param>
        /// <param name="line">The line of the source of the error.</param>
        /// <param name="column">The column of the source of the error.</param>
        /// <param name="errorNumber">The error number of the error.</param>
        /// <param name="errorText">The error message text.</param>
        public GenerationError(
            string fileName,
            int line,
            int column,
            string errorNumber,
            string errorText)
        {
            this.FileName = fileName;
            this.Line = line;
            this.Column = column;
            this.ErrorNumber = errorNumber;
            this.ErrorText = errorText;
        }

        /// <summary>Gets or sets the column number where the source of the error occurs.</summary>
        /// <returns>The column number of the source file where the compiler encountered the error.</returns>
        public int Column { get; set; }

        /// <summary>Gets or sets the error number.</summary>
        /// <returns>The error number as a string.</returns>
        public string ErrorNumber { get; set; }

        /// <summary>Gets or sets the text of the error message.</summary>
        /// <returns>The text of the error message.</returns>
        public string ErrorText { get; set; }

        /// <summary>Gets or sets the file name of the source file that contains the code which caused the error.</summary>
        /// <returns>The file name of the source file that contains the code which caused the error.</returns>
        public string FileName { get; set; }

        /// <summary>Gets or sets a value that indicates whether the error is a warning.</summary>
        /// <returns>
        /// <see langword="true" /> if the error is a warning; otherwise, <see langword="false" />.</returns>
        public bool IsWarning { get; set; }

        /// <summary>Gets or sets the line number where the source of the error occurs.</summary>
        /// <returns>The line number of the source file where the compiler encountered the error.</returns>
        public int Line { get; set; }

        /// <summary>Provides an implementation of Object's <see cref="M:System.Object.ToString" /> method.</summary>
        /// <returns>A string representation of the compiler error.</returns>
        public override string ToString()
        {
            return $"{this.FileName}:{this.Line},{this.Column} {(this.IsWarning ? Warning : Error)} {this.ErrorNumber} {this.ErrorText}";
        }
    }
}