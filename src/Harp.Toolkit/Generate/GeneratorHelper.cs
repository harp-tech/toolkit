using System.CodeDom.Compiler;
using System.Text;

namespace Harp.Toolkit.Generate;

public static class GeneratorHelper
{
    public static bool AssertNoGeneratorErrors(CompilerErrorCollection errors)
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
            Console.Error.WriteLine(errorLog.ToString());
            return !errors.HasErrors;
        }

        return true;
    }
}
