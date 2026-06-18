// Mapping from backend ApiResponse<T>
export interface ApiResponse<T> {
  statusCode: number;
  message: string;
  data: T;
  responsedAt: string;
}

// Mapping from backend AuthResult
export interface AuthResult {
  success: boolean;
  accessToken: string | null;
  refreshToken: string | null;
  expiresAt: string | null;
  errors: string[];
}

// Mapping from backend LoginRequest
export interface LoginRequest {
  email: string;
  password: string;
}

// Mapping from backend GoogleLoginRequest
export interface GoogleLoginRequest {
  idToken: string;
}

// Decoded JWT payload (from IamService token claims)
export interface JwtPayload {
  sub: string; // User ID (Guid)
  email: string;
  name?: string;
  role: string; // "Admin" | "Lecture"
  jti: string; // JWT ID
  exp: number; // Expiry timestamp
  iss: string;
  aud: string;
}
