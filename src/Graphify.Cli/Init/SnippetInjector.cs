namespace Graphify.Cli.Init;

public static class SnippetInjector
{
    /// <summary>
    /// Write the canonical instructions to .graphify/mcp-instructions.md.
    /// Returns true if written, false if skipped (already exists and not forced).
    /// </summary>
    public static bool WriteCanonicalInstructions(string projectDir, bool force)
    {
        var graphifyDir = Path.Combine(projectDir, ".graphify");
        var instructionsPath = Path.Combine(graphifyDir, "mcp-instructions.md");

        if (File.Exists(instructionsPath) && !force)
            return false;

        Directory.CreateDirectory(graphifyDir);
        File.WriteAllText(instructionsPath, InstructionTemplate.CanonicalInstructions);
        return true;
    }

    /// <summary>
    /// Inject the pointer snippet into an agent's instruction file.
    /// Returns true if injected, false if already present.
    /// </summary>
    public static bool InjectSnippet(string filePath)
    {
        if (!File.Exists(filePath))
        {
            // Create the file and write the snippet
            var dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            File.WriteAllText(filePath, InstructionTemplate.SnippetPointer + Environment.NewLine);
            return true;
        }

        var content = File.ReadAllText(filePath);

        // Check if already injected
        if (content.Contains(InstructionTemplate.SectionStartMarker))
            return false;

        // Append snippet to file
        var trimmed = content.TrimEnd();
        File.WriteAllText(filePath, trimmed + Environment.NewLine + Environment.NewLine + InstructionTemplate.SnippetPointer + Environment.NewLine);
        return true;
    }

    /// <summary>
    /// Remove the graphify snippet from an agent's instruction file.
    /// Returns true if removed, false if not found.
    /// </summary>
    public static bool RemoveSnippet(string filePath)
    {
        if (!File.Exists(filePath))
            return false;

        var content = File.ReadAllText(filePath);
        var startIdx = content.IndexOf(InstructionTemplate.SectionStartMarker);
        var endIdx = content.IndexOf(InstructionTemplate.SectionEndMarker);

        if (startIdx < 0 || endIdx < 0)
            return false;

        // Remove from start marker to end marker (inclusive), plus surrounding whitespace
        var endOfEnd = endIdx + InstructionTemplate.SectionEndMarker.Length;
        var before = content[..startIdx].TrimEnd();
        var after = content[endOfEnd..].TrimStart();

        var newContent = before + Environment.NewLine + Environment.NewLine + after;
        File.WriteAllText(filePath, newContent.TrimStart() + Environment.NewLine);
        return true;
    }

    /// <summary>
    /// Remove the canonical instructions file.
    /// </summary>
    public static bool RemoveCanonicalInstructions(string projectDir)
    {
        var instructionsPath = Path.Combine(projectDir, ".graphify", "mcp-instructions.md");
        if (!File.Exists(instructionsPath))
            return false;

        File.Delete(instructionsPath);

        // Remove .graphify directory if empty
        var graphifyDir = Path.GetDirectoryName(instructionsPath);
        if (graphifyDir != null && !Directory.EnumerateFileSystemEntries(graphifyDir).Any())
            Directory.Delete(graphifyDir);

        return true;
    }
}
