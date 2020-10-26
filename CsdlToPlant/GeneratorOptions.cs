namespace CsdlToPlant
{
    using System;
    using System.Collections.Generic;
    using System.Text;

    /// <summary>
    /// Options to configure generation
    /// </summary>
    public class GeneratorOptions
    {
        /// <summary>
        /// Generator options used if no custom value specified.
        /// </summary>
        public static GeneratorOptions DefaultGeneratorOptions = new GeneratorOptions {SkipList = new[] {"Entity"}};

        /// <summary>
        /// List of names of types to skip over when generating.
        /// </summary>
        public IEnumerable<string> SkipList { get; set; }
    }
}
