namespace cap_theorem_backend.DTOs.Auth;

// Único DTO de auth que sigue aplicando: solo hay login/registro vía OAuth
// (Google/GitHub), no hay email+password (ver Segunda_entrega).
public record AuthResponse(string Token, DateTime ExpiresAt);
