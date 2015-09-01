using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;

using EnvDTE;
using VSLangProj;
using Microsoft.VisualStudio.TextTemplating.VSHost;

namespace RoslynGenerator
{
	public abstract class BaseCustomToolGenerator : BaseCodeGeneratorWithSite
	{
		private const string ProjectFullPathItem = "FullPath";
		private const string ProjectOutputPathItem = "OutputPath";

		protected abstract string Generate(string fname, string text);

		protected string GetCurrentProjectOutputPath()
		{
			var project = GetProject();
			var fullPath = GetFullPath(project);
			var outputPath = GetOutputPath(project);

			return Path.Combine(fullPath, outputPath);
		}

		protected override byte[] GenerateCode(string inputFileName, string inputFileContent)
		{
			try
			{
				var text = Generate(inputFileName, inputFileContent);
				return Encoding.UTF8.GetBytes(text);
			}
			catch (Exception ex)
			{
				ShowException(ex);
				return Encoding.UTF8.GetBytes("Error");
			}
		}

		private void ShowErrorWindow()
		{
			ErrorList.BringToFront();
			ErrorList.ForceShowErrors();
		}

		protected void ShowException(Exception e)
		{
			var message = CreateExceptionMessage(e);
			GeneratorErrorCallback(false, 1, message, 0, 0);
			ShowErrorWindow();
		}

		private static string GetOutputPath(Project project) => project?.ConfigurationManager?.ActiveConfiguration?.Properties?.Item(ProjectOutputPathItem)?.Value?.ToString();

		private static string GetFullPath(Project project) => project?.Properties?.Item(ProjectFullPathItem)?.Value?.ToString();

		protected ProjectItem GetProjectItem() => (ProjectItem)GetService(typeof(ProjectItem));

		protected Project GetProject() => GetProjectItem()?.ContainingProject;

		protected VSProject GetVsProject() => (VSProject)GetProject()?.Object;
	}
}
