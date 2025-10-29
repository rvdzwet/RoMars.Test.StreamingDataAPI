using System.Text.Json.Serialization;
using RoMars.DataStreaming.Json.Attributes;

namespace RoMars.StreamingJsonOutput.Framework.Models
{
    /// <summary>
    /// Represents the full document metadata, structured for JSON output.
    /// This interface defines the contract for streaming data from a DbDataReader
    /// directly to JSON, using custom attributes for fine-grained control over
    /// column mapping, flattening of nested objects, and array creation from
    /// patterned columns, without instantiating intermediate objects per row.
    /// </summary>
    public interface IDocumentMetadataDto
    {
        // Core Columns
        [JsonPropertyName("document_id")]
        [DataReaderColumn("DocumentId")]
        long DocumentId { get; }

        [JsonPropertyName("document_title")]
        [DataReaderColumn("DocumentTitle")]
        string? DocumentTitle { get; }

        [JsonPropertyName("mortgage_amount")]
        [DataReaderColumn("MortgageAmount")]
        decimal MortgageAmount { get; }

        // Customer Information (nested object)
        [JsonPropertyName("customer")]
        ICustomerDto Customer { get; }

        // Loan Information
        [JsonPropertyName("loan_number")]
        [DataReaderColumn("LoanNumber")]
        string? LoanNumber { get; }

        [JsonPropertyName("interest_rate")]
        [DataReaderColumn("InterestRate")]
        decimal InterestRate { get; }

        [JsonPropertyName("loan_term_months")]
        [DataReaderColumn("LoanTermMonths")]
        int LoanTermMonths { get; }

        [JsonPropertyName("credit_score")]
        [DataReaderColumn("CreditScore")]
        int CreditScore { get; }

        [JsonPropertyName("ltv_ratio")]
        [DataReaderColumn("LTVRatio")]
        decimal LTVRatio { get; }


        // Property Information (nested object)
        [JsonPropertyName("property")]
        IPropertyDto Property { get; }

        [JsonPropertyName("property_appraisal_value")]
        [DataReaderColumn("PropertyAppraisalValue")]
        decimal PropertyAppraisalValue { get; }

        [JsonPropertyName("insurance_premium")]
        [DataReaderColumn("InsurancePremium")]
        decimal InsurancePremium { get; }

        [JsonPropertyName("property_tax_amount")]
        [DataReaderColumn("PropertyTaxAmount")]
        decimal PropertyTaxAmount { get; }


        // Document Details
        [JsonPropertyName("document_type")]
        [DataReaderColumn("DocumentType")]
        string? DocumentType { get; }

        [JsonPropertyName("file_type")]
        [DataReaderColumn("FileType")]
        string? FileType { get; }

        [JsonPropertyName("page_count")]
        [DataReaderColumn("PageCount")]
        int PageCount { get; }

        [JsonPropertyName("document_size_kb")]
        [DataReaderColumn("DocumentSizeKB")]
        int DocumentSizeKB { get; }

        [JsonPropertyName("original_filename")]
        [DataReaderColumn("OriginalFilename")]
        string? OriginalFilename { get; }

        [JsonPropertyName("source_system")]
        [DataReaderColumn("SourceSystem")]
        string? SourceSystem { get; }

        [JsonPropertyName("version_number")]
        [DataReaderColumn("VersionNumber")]
        int VersionNumber { get; }

        [JsonPropertyName("document_hash_crc32")]
        [DataReaderColumn("DocumentHash_CRC32")]
        long DocumentHashCrc32 { get; } // Renamed for clarity in JSON

        [JsonPropertyName("document_hash_md5")]
        [DataReaderColumn("DocumentHash_MD5")]
        string? DocumentHashMd5 { get; } // Renamed for clarity in JSON

        [JsonPropertyName("document_score")]
        [DataReaderColumn("DocumentScore")]
        int DocumentScore { get; }


        // Workflow and Review
        [JsonPropertyName("workflow_status")]
        [DataReaderColumn("WorkflowStatus")]
        string? WorkflowStatus { get; }

        [JsonPropertyName("reviewer_name")]
        [DataReaderColumn("ReviewerName")]
        string? ReviewerName { get; }

        [JsonPropertyName("processing_time_minutes")]
        [DataReaderColumn("ProcessingTimeMinutes")]
        int ProcessingTimeMinutes { get; }

        [JsonPropertyName("compliance_score")]
        [DataReaderColumn("ComplianceScore")]
        decimal ComplianceScore { get; }

        [JsonPropertyName("risk_rating")]
        [DataReaderColumn("RiskRating")]
        int RiskRating { get; }

        [JsonPropertyName("audit_count")]
        [DataReaderColumn("AuditCount")]
        int AuditCount { get; }

        [JsonPropertyName("associated_fees")]
        [DataReaderColumn("AssociatedFees")]
        decimal AssociatedFees { get; }

        [JsonPropertyName("escrow_balance")]
        [DataReaderColumn("EscrowBalance")]
        decimal EscrowBalance { get; }


        // Dates
        [JsonPropertyName("creation_date")]
        [DataReaderColumn("CreationDate")]
        DateTime CreationDate { get; }

        [JsonPropertyName("last_modified_date")]
        [DataReaderColumn("LastModifiedDate")]
        DateTime LastModifiedDate { get; }

        [JsonPropertyName("review_date")]
        [DataReaderColumn("ReviewDate")]
        DateTime ReviewDate { get; }

        [JsonPropertyName("approval_date")]
        [DataReaderColumn("ApprovalDate")]
        DateTime ApprovalDate { get; }

        [JsonPropertyName("expiration_date")]
        [DataReaderColumn("ExpirationDate")]
        DateTime ExpirationDate { get; }

        [JsonPropertyName("original_upload_date")]
        [DataReaderColumn("OriginalUploadDate")]
        DateTime OriginalUploadDate { get; }

        [JsonPropertyName("last_accessed_date")]
        [DataReaderColumn("LastAccessedDate")]
        DateTime LastAccessedDate { get; }

        [JsonPropertyName("retention_end_date")]
        [DataReaderColumn("RetentionEndDate")]
        DateTime RetentionEndDate { get; }

        [JsonPropertyName("next_review_date")]
        [DataReaderColumn("NextReviewDate")]
        DateTime NextReviewDate { get; }

        [JsonPropertyName("funding_date")]
        [DataReaderColumn("FundingDate")]
        DateTime FundingDate { get; }

        [JsonPropertyName("disbursement_date")]
        [DataReaderColumn("DisbursementDate")]
        DateTime DisbursementDate { get; }

        [JsonPropertyName("closing_date")]
        [DataReaderColumn("ClosingDate")]
        DateTime ClosingDate { get; }

        [JsonPropertyName("effective_date_01")]
        [DataReaderColumn("EffectiveDate_01")]
        DateTime EffectiveDate01 { get; }

        [JsonPropertyName("effective_date_02")]
        [DataReaderColumn("EffectiveDate_02")]
        DateTime EffectiveDate02 { get; }

        [JsonPropertyName("effective_date_03")]
        [DataReaderColumn("EffectiveDate_03")]
        DateTime EffectiveDate03 { get; }

        [JsonPropertyName("retention_years")]
        [DataReaderColumn("RetentionYears")]
        int RetentionYears { get; }


        // Arrays from patterned columns
        [JsonPropertyName("tags")]
        [DataReaderArrayPattern("Tag_")]
        // Property type must be IEnumerable<string> as we are collecting strings
        IEnumerable<string>? Tags { get; }

        [JsonPropertyName("comments")]
        [DataReaderArrayPattern("Comment_")]
        // Property type must be IEnumerable<string> as we are collecting strings
        IEnumerable<string>? Comments { get; }
    }

    /// <summary>
    /// Defines the customer portion of the DocumentMetadataDto.
    /// This is an interface to be used as a nested JSON object.
    /// </summary>
    public interface ICustomerDto
    {
        [JsonPropertyName("name")]
        [DataReaderColumn("CustomerName")]
        string? Name { get; }

        [JsonPropertyName("address")]
        [JsonFlatten] // Flatten customer address into the customer object
        ICustomerAddressDto Address { get; }
    }

    /// <summary>
    /// Defines the property portion of the DocumentMetadataDto.
    /// This is an interface to be used as a nested JSON object.
    /// </summary>
    public interface IPropertyDto
    {
        [JsonPropertyName("street_address")]
        [JsonFlatten] // Flatten property address into the property object
        IPropertyAddressDto StreetAddress { get; }
    }
}
