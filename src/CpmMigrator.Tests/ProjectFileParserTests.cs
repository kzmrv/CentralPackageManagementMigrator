using System.Xml;

namespace CpmMigrator.Tests
{
    public class ProjectFileParserTests
    {
        [Test]
        public void TestSampleProjectParsing()
        {
            var packageVersions = ProjectFileParser.ParseProjectFile(new MigratorCommand.Settings(), "SampleCsproj", FromTestFile("Samples\\SampleCsproj.txt"));
            Assert.That(packageVersions.Count, Is.EqualTo(5));
            Assert.That(packageVersions["NUnit"].Version, Is.EqualTo("3.13.3"));
        }

        private XmlDocument FromTestFile(string testFileName)
        {
            var directory = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            var path = System.IO.Path.Combine(directory!, testFileName);
            var projectDocument = new XmlDocument();
            projectDocument.Load(path);
            return projectDocument;
        } 
    }
}