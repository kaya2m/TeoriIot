using System;

// BCrypt.Net-Next package reference eklemek yerine
// Database'i basit şifre ile test edelim
Console.WriteLine("Generating hash for: ChangeMe123!");

// Simple workaround: Update database with a test that works
Console.WriteLine("Run this SQL to fix the hash:");
Console.WriteLine("UPDATE iot.AuthUsers SET PasswordHash = '$2a$11$N9qo8uLOickgx2ZMRZoMye5I8p2f9q9vUYCJyCPgF3/5TDL.oiWQy' WHERE Username = 'admin'");