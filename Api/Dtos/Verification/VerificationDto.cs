namespace Api.Dtos.Verification
{
    public class SubmitVerificationDto
    {
        public string TargetRole { get; set; } = string.Empty; // Student, Health, Adult
        public string? DocumentType { get; set; }
        public string? Notes { get; set; }
    }

    public class ReviewVerificationDto
    {
        public string Decision { get; set; } = string.Empty; // Approved, Rejected
        public string? Reason { get; set; }
    }

    public class VerificationResponseDto
    {
        public Guid Id { get; set; }
        public string UserId { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string UserEmail { get; set; } = string.Empty;
        public string TargetRole { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string? ReviewNotes { get; set; }
        public DateTime SubmittedAt { get; set; }
        public DateTime? ReviewedAt { get; set; }
    }
}
