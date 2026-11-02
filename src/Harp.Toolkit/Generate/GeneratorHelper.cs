using System.CodeDom.Compiler;
using System.Text;

namespace Harp.Toolkit.Generate;

public static class GeneratorHelper
{
    public static void AssertNoGeneratorErrors(CompilerErrorCollection errors)
    {
        if (errors.Count > 0)
        {
            var errorLog = new StringBuilder();
            errorLog.AppendLine("Code generation has completed with errors:");
            foreach (CompilerError error in errors)
            {
                var warningString = error.IsWarning ? "warning" : "error";
                errorLog.AppendLine($"{error.FileName}: {warningString}: {error.ErrorText}");
            }

            var message = errorLog.ToString();
            if (errors.HasErrors)
            {
                var exception = new InvalidOperationException(message);
                exception.SetDescription(message);
                throw exception;
            }

            Console.Error.WriteLine(message);
        }
    }
}
