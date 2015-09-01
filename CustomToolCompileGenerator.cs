using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Shell;
using VSLangProj;
using VSLangProj80;

namespace RoslynGenerator
{
	[ComVisible(true)]
	[Guid("74849c41-a14b-4ff8-8bf2-584a94043032")]
	[CodeGeneratorRegistration(typeof(RoslynGeneratorPackage), "RoslynGenerator", vsContextGuids.vsContextGuidVCSProject, GeneratesDesignTimeSource = true, GeneratorRegKeyName = "RoslynGenerator")]
	[ProvideObject(typeof(RoslynGeneratorPackage))]
	[ClassInterface(ClassInterfaceType.None)]
	public sealed class RoslynGeneratorPackage : BaseCustomToolGenerator
	{
		private const string DefaultExtension = ".gen.cs";
		private const string GeneratorClassName = "MetaSyntaxWalker";

		public override string GetDefaultExtension() => DefaultExtension;

		protected override string Generate(string fname, string text)
		{
			try
			{
				return GenerateAsync(fname).Result;
			}
			catch (Exception ex)
			{
				return $"/*{ex}*/";
			}
		}

		private async Task<string> GenerateAsync(string fname)
		{
			var solution = GetSolution();
			var doc = GetDocument(solution, fname);
			var syntaxTree = await doc.GetSyntaxTreeAsync();
			var semanticModel = await doc.GetSemanticModelAsync();

			var root = syntaxTree.GetRoot();
			root = Construct(root, semanticModel);

			//doesn't always work as expected
			root = Formatter.Format(root, solution.Workspace);

			var newSyntaxTree = CSharpSyntaxTree.Create((CSharpSyntaxNode)root);

			return newSyntaxTree.GetText().ToString();
		}

		private CompilationUnitSyntax Construct(SyntaxNode root, SemanticModel semanticModel)
		{
			var references = GetReferences();
			var generator = GetType(references, GeneratorClassName);

			var cu = generator.Generate(semanticModel, root);

			return cu;
		}

		private dynamic GetType(IEnumerable<string> references, string name)
		{
			foreach (var reference in references)
			{
				try
				{
					AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_AssemblyResolve;

					var assembly = LoadAssemblyFromFile(reference);
					var types = assembly.GetTypes();

					AppDomain.CurrentDomain.AssemblyResolve -= CurrentDomain_AssemblyResolve;

					var type = types.FirstOrDefault(a => a.Name == name);

					if (type != null)
					{
						return Activator.CreateInstance(type);
					}
				}
				catch (Exception ex)
				{
					ShowException(ex);
				}
			}

			throw new ArgumentOutOfRangeException(nameof(name));
		}

		private Assembly LoadAssemblyFromFile(string path)
		{
			var data = File.ReadAllBytes(path);
			var pdbData = default(byte[]);

			var pdbFileName = Path.Combine(Path.GetDirectoryName(path), Path.GetFileNameWithoutExtension(path) + ".pdb");

			//despite this, pdb file is still locked in experimental hive debug mode
			if (File.Exists(pdbFileName))
				pdbData = File.ReadAllBytes(pdbFileName);

			var assembly = Assembly.Load(data, pdbData);

			return assembly;
		}

		private Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args)
		{
			if (args.Name == Assembly.GetExecutingAssembly().FullName)
				return Assembly.GetExecutingAssembly();

			var references = GetReferences();

			var an = new AssemblyName(args.Name);
			var fullPath = references.First(r => r.IndexOf(an.Name, StringComparison.OrdinalIgnoreCase) != -1);

			return LoadAssemblyFromFile(fullPath);
		}

		private static Document GetDocument(Solution solution, string fname)
		{
			var docId = solution.GetDocumentIdsWithFilePath(fname).First();
			var doc = solution.GetDocument(docId);

			return doc;
		}

		private static Solution GetSolution()
		{
			var componentModel = (IComponentModel)Package.GetGlobalService(typeof(SComponentModel));
			var workspace = componentModel.GetService<Microsoft.VisualStudio.LanguageServices.VisualStudioWorkspace>();
			var solution = workspace.CurrentSolution;

			return solution;
		}

		private IEnumerable<string> GetReferences()
		{
			return GetVsProject()
				.References
				.Cast<Reference>()
				.Select(a => a.Path)
				.Where(a => !a.Contains(".NETFramework"));
		}
	}
}
