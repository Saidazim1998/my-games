// Soxta "Unity build" — launcher testlari uchun
const string Version = "1.0.0";
Console.WriteLine($"DummyGame v{Version} ishga tushdi!");
Console.WriteLine($"Ish papkasi: {Environment.CurrentDirectory}");
File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "launched.txt"),
    $"Launched at {DateTime.Now:O}");
Console.WriteLine("Yopish uchun istalgan tugmani bosing...");
Console.ReadKey();
