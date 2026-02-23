using Daiv3.Infrastructure.Shared.Hardware;

Console.WriteLine();
Console.WriteLine("Running Hardware Detection Test on Windows 11 Copilot+ PC");
Console.WriteLine("=========================================================");
Console.WriteLine();

// Debug info
Console.WriteLine("DEBUG: Active conditional symbols:");
#if NET10_0_WINDOWS10_0_26100_OR_GREATER
Console.WriteLine("  ✓ NET10_0_WINDOWS10_0_26100_OR_GREATER is ACTIVE");
#else
Console.WriteLine("  ✗ NET10_0_WINDOWS10_0_26100_OR_GREATER is NOT active");
#endif
Console.WriteLine();

try
{
    HardwareDetectionDemo.Run();
    return 0;
}
catch (Exception ex)
{
    Console.WriteLine($"Error: {ex.Message}");
    Console.WriteLine(ex.StackTrace);
    return 1;
}
