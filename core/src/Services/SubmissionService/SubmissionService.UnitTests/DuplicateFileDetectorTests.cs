using System;
using System.Collections.Generic;
using System.Linq;
using SubmissionService.Application;
using Xunit;

namespace SubmissionService.UnitTests
{
    public class DuplicateFileDetectorTests
    {
        [Fact]
        public void Detect_WhenNoFiles_ReturnsEmpty()
        {
            var result = DuplicateFileDetector.Detect(Array.Empty<HashedFileEntry>());

            Assert.Empty(result);
        }

        [Fact]
        public void Detect_WhenAllHashesUnique_ReturnsEmpty()
        {
            var paperA = Guid.NewGuid();
            var paperB = Guid.NewGuid();
            var files = new[]
            {
                new HashedFileEntry("hash-1", paperA, "Student_0001", "a.pdf"),
                new HashedFileEntry("hash-2", paperB, "Student_0002", "b.pdf"),
            };

            var result = DuplicateFileDetector.Detect(files);

            Assert.Empty(result);
        }

        [Fact]
        public void Detect_WhenTwoFilesShareHash_ReturnsOneWarningWithBothEntries()
        {
            var paperA = Guid.NewGuid();
            var paperB = Guid.NewGuid();
            var files = new[]
            {
                new HashedFileEntry("same-hash", paperA, "Student_0001", "a.pdf"),
                new HashedFileEntry("same-hash", paperB, "Student_0002", "b.pdf"),
            };

            var result = DuplicateFileDetector.Detect(files);

            var warning = Assert.Single(result);
            Assert.Equal("same-hash", warning.ContentHash);
            Assert.Equal(2, warning.Files.Count);
            Assert.Contains(warning.Files, f => f.PaperId == paperA && f.FileName == "a.pdf");
            Assert.Contains(warning.Files, f => f.PaperId == paperB && f.FileName == "b.pdf");
        }

        [Fact]
        public void Detect_WhenSameStudentUploadsIdenticalFileTwice_StillFlagsAsDuplicate()
        {
            // Two different files within the SAME paper that happen to have identical
            // content (e.g. the student included the same scan twice) should also warn.
            var paper = Guid.NewGuid();
            var files = new[]
            {
                new HashedFileEntry("same-hash", paper, "Student_0001", "page1.pdf"),
                new HashedFileEntry("same-hash", paper, "Student_0001", "page1-copy.pdf"),
            };

            var result = DuplicateFileDetector.Detect(files);

            var warning = Assert.Single(result);
            Assert.Equal(2, warning.Files.Count);
        }

        [Fact]
        public void Detect_WhenMultipleDistinctDuplicateGroupsExist_ReturnsOneWarningPerGroup()
        {
            var files = new[]
            {
                new HashedFileEntry("hash-A", Guid.NewGuid(), "Student_0001", "a1.pdf"),
                new HashedFileEntry("hash-A", Guid.NewGuid(), "Student_0002", "a2.pdf"),
                new HashedFileEntry("hash-B", Guid.NewGuid(), "Student_0003", "b1.pdf"),
                new HashedFileEntry("hash-B", Guid.NewGuid(), "Student_0004", "b2.pdf"),
                new HashedFileEntry("hash-B", Guid.NewGuid(), "Student_0005", "b3.pdf"),
                new HashedFileEntry("hash-C", Guid.NewGuid(), "Student_0006", "c1.pdf"),
            };

            var result = DuplicateFileDetector.Detect(files);

            Assert.Equal(2, result.Count);
            var groupA = result.Single(w => w.ContentHash == "hash-A");
            var groupB = result.Single(w => w.ContentHash == "hash-B");
            Assert.Equal(2, groupA.Files.Count);
            Assert.Equal(3, groupB.Files.Count);
        }

        [Fact]
        public void Detect_HashComparisonIsCaseSensitive()
        {
            // Content hashes are lowercase hex from SHA-256; detection must not
            // accidentally fold case and merge unrelated groups.
            var files = new[]
            {
                new HashedFileEntry("abc123", Guid.NewGuid(), "Student_0001", "a.pdf"),
                new HashedFileEntry("ABC123", Guid.NewGuid(), "Student_0002", "b.pdf"),
            };

            var result = DuplicateFileDetector.Detect(files);

            Assert.Empty(result);
        }
    }
}
