namespace EncyPcCheck;

internal static class AppInfo
{
    public const string Version = "0.4.6";
    public const string WebsiteUrl = "https://www.cnc-technik.de";

    // Oeffentliche Supabase-Adresse (DTR, Hostinger) und ANON-Schluessel.
    // Der Anon-Schluessel darf im ausgelieferten Programm stehen; er wirkt nur zusammen mit den
    // Zeilenrechten (RLS) der Tabelle dtr_pc_scores. NIEMALS den Service-Schluessel eintragen.
    public const string SupabaseUrl = "https://supabase.cnc-technik.cloud";
    public const string SupabaseAnonKey = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJyb2xlIjoiYW5vbiIsImlzcyI6InN1cGFiYXNlIiwiaWF0IjoxNzkyNzUwNzg5LCJleHAiOjE5NTA0MzA3ODl9.sD1oI5Axjl1Z9-kHewEVR0amXetEhbDhpxHxtvxqX1o";
}
