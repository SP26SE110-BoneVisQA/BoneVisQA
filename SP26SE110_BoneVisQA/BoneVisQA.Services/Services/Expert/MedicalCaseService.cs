using BoneVisQA.Repositories.Models;
using BoneVisQA.Repositories.UnitOfWork;
using BoneVisQA.Services.Helpers;
using BoneVisQA.Services.Interfaces;
using BoneVisQA.Services.Interfaces.Expert;
using BoneVisQA.Services.Models.Expert;
using BoneVisQA.Services.Services;
using BoneVisQA.Services.Models.Student;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace BoneVisQA.Services.Services.Expert
{
    public class MedicalCaseService : IMedicalCaseService
    {
        private static readonly Regex SemanticVersionRegex = new(@"^\s*(\d+)\.(\d+)\.(\d+)\s*$", RegexOptions.Compiled);
        private readonly IUnitOfWork _unitOfWork;
        private readonly IWebHostEnvironment _env;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IConfiguration _configuration;
        private readonly ISupabaseStorageService _storageService;
        private const string MedicalImagesBucket = "medical-images";

        public MedicalCaseService(
            IUnitOfWork unitOfWork,
            IWebHostEnvironment env,
            IHttpContextAccessor httpContextAccessor,
            IConfiguration configuration,
            ISupabaseStorageService storageService)
        {
            _unitOfWork = unitOfWork;
            _env = env;
            _httpContextAccessor = httpContextAccessor;
            _configuration = configuration;
            _storageService = storageService;
        }
        private async Task<string> SaveImageAsync(IFormFile file, Guid caseId)
        {
            // Generate unique filename with case ID for organization
            var extension = Path.GetExtension(file.FileName);
            var fileName = $"{caseId}/{Guid.NewGuid()}{extension}";

            // Upload to Supabase Storage
            var imageUrl = await _storageService.UploadFileToPathAsync(file, MedicalImagesBucket, fileName);

            return imageUrl;
        }
        public async Task<PagedResult<GetMedicalCaseDTO>> GetAllMedicalCasesAsync(int pageIndex, int pageSize, Guid? expertId = null)
        {
            var query = _unitOfWork.MedicalCaseRepository.GetQueryable().AsNoTracking();
            if (expertId.HasValue)
            {
                query = query.Where(x =>
                    x.CreatedByExpertId == expertId.Value ||
                    x.ValidatedByUserId == expertId.Value);
            }

            var totalCount = await query.CountAsync();

            var entities = await query
                .Include(x => x.Category)
                .Include(x => x.CreatedByExpert)
                .Include(x => x.ValidatedByUser)
                .Include(x => x.CaseTags)
                    .ThenInclude(ct => ct.Tag)
                .Include(x => x.CaseMetadata)
                .Include(x => x.MedicalImages)
                .OrderByDescending(x => x.CreatedAt)
                .Skip((pageIndex - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var medicalCases = entities.Select(x =>
            {
                var anatomySite = ExpertMedicalCaseDisplayHelper.ResolveAnatomySite(x);
                var pathologyGroup = ExpertMedicalCaseDisplayHelper.ResolvePathologyGroup(x);
                return new GetMedicalCaseDTO
                {
                    Id = x.Id,
                    Title = x.Title,
                    CreatedByExpertId = x.CreatedByExpertId,
                    ExpertName = x.CreatedByExpert?.FullName ?? x.ValidatedByUser?.FullName,
                    Description = x.Description,
                    Difficulty = x.Difficulty,
                    CategoryId = x.CategoryId,
                    CategoryName = x.Category?.Name,
                    AnatomySite = anatomySite,
                    PathologyGroup = pathologyGroup,
                    BoneLocation = anatomySite,
                    CaseOrigin = CaseOriginHelper.ResolveExpertCaseOrigin(x.CaseTags),
                    IsApproved = x.IsApproved,
                    IsActive = x.IsActive,
                    SuggestedDiagnosis = x.SuggestedDiagnosis,
                    KeyFindings = x.KeyFindings,
                    CreatedAt = x.CreatedAt,
                    UpdatedAt = x.UpdatedAt,
                    ThumbnailUrl = x.MedicalImages
                        .OrderBy(m => m.CreatedAt ?? DateTime.MinValue)
                        .ThenBy(m => m.Id)
                        .Select(m => m.ImageUrl)
                        .FirstOrDefault()
                        ?? string.Empty
                };
            }).ToList();

            foreach (var row in medicalCases)
                ExpertMedicalCaseDisplayHelper.ApplyListDefaults(row, expertScoped: expertId.HasValue);

            return new PagedResult<GetMedicalCaseDTO>
            {
                Items = medicalCases,
                TotalCount = totalCount,
                PageIndex = pageIndex,
                PageSize = pageSize
            };
        }

        public async Task<GetExpertMedicalCaseDetailDto?> GetMedicalCaseByIdAsync(Guid id, Guid? expertId = null)
        {
            var query = _unitOfWork.MedicalCaseRepository.GetQueryable()
                .AsNoTracking()
                .Where(c => c.Id == id);
            if (expertId.HasValue)
            {
                query = query.Where(c =>
                    c.CreatedByExpertId == expertId.Value ||
                    c.ValidatedByUserId == expertId.Value);
            }

            var entity = await query
                .Include(c => c.Category)
                .Include(c => c.CreatedByExpert)
                .Include(c => c.ValidatedByUser)
                .Include(c => c.CaseTags)
                    .ThenInclude(ct => ct.Tag)
                .Include(c => c.MedicalImages)
                    .ThenInclude(m => m.CaseAnnotations)
                .Include(c => c.CaseMedia)
                .Include(c => c.CaseMetadata)
                .FirstOrDefaultAsync();

            if (entity == null)
                return null;

            var anatomySite = ExpertMedicalCaseDisplayHelper.ResolveAnatomySite(entity);
            var pathologyGroup = ExpertMedicalCaseDisplayHelper.ResolvePathologyGroup(entity);

            var dto = new GetExpertMedicalCaseDetailDto
            {
                Id = entity.Id,
                Title = entity.Title,
                CreatedByExpertId = entity.CreatedByExpertId,
                ExpertName = entity.CreatedByExpert?.FullName ?? entity.ValidatedByUser?.FullName,
                Description = entity.Description,
                Difficulty = entity.Difficulty,
                CategoryId = entity.CategoryId,
                CategoryName = entity.Category?.Name,
                AnatomySite = anatomySite,
                PathologyGroup = pathologyGroup,
                BoneLocation = anatomySite,
                CaseOrigin = CaseOriginHelper.ResolveExpertCaseOrigin(entity.CaseTags),
                IsApproved = entity.IsApproved,
                IsActive = entity.IsActive,
                SuggestedDiagnosis = entity.SuggestedDiagnosis,
                KeyFindings = entity.KeyFindings,
                ReflectiveQuestions = entity.ReflectiveQuestions,
                CreatedAt = entity.CreatedAt,
                UpdatedAt = entity.UpdatedAt,
                MedicalImages = entity.MedicalImages
                    .OrderByDescending(m => m.CreatedAt)
                    .Select(m => new ExpertMedicalCaseImageSummaryDto
                    {
                        Id = m.Id,
                        ImageUrl = m.ImageUrl,
                        Modality = m.Modality,
                        CreatedAt = m.CreatedAt,
                        Annotations = m.CaseAnnotations
                            .OrderBy(a => a.CreatedAt ?? DateTime.MinValue)
                            .Select(a => new ExpertCaseAnnotationSummaryDto
                            {
                                Id = a.Id,
                                Label = a.Label,
                                Coordinates = BoundingBoxParser.CanonicalizeOrOriginal(a.Coordinates),
                            })
                            .ToList(),
                    })
                    .ToList(),
                Tags = entity.CaseTags
                    .Where(ct => ct.Tag != null)
                    .Select(ct => new ExpertCaseTagSummaryDto
                    {
                        Id = ct.Tag!.Id,
                        Name = ct.Tag.Name,
                        Type = ct.Tag.Type
                    })
                    .ToList(),
                ThumbnailUrl = entity.MedicalImages
                    .OrderBy(m => m.CreatedAt ?? DateTime.MinValue)
                    .ThenBy(m => m.Id)
                    .Select(m => m.ImageUrl)
                    .FirstOrDefault()
                    ?? CaseMediaDicomMetadataHelper.ResolveFirstPreviewUrl(entity)
                    ?? string.Empty,
                DicomMetadata = CaseMediaDicomMetadataHelper.ResolveFirstMetadata(entity),
                Metadata = entity.CaseMetadata == null
                    ? null
                    : new ExpertCaseMetadataSummaryDto
                    {
                        Modality = entity.CaseMetadata.Modality,
                        Anatomy = entity.CaseMetadata.Anatomy,
                        AnatomySite = entity.CaseMetadata.AnatomySite,
                        PathologyGroup = entity.CaseMetadata.PathologyGroup,
                        Laterality = entity.CaseMetadata.Laterality,
                        ViewPosition = entity.CaseMetadata.ViewPosition,
                        Difficulty = entity.CaseMetadata.Difficulty,
                        SourceType = entity.CaseMetadata.SourceType,
                        QualityScore = entity.CaseMetadata.QualityScore,
                        SuggestedDiagnosis = entity.CaseMetadata.SuggestedDiagnosis,
                    },
            };

            ExpertMedicalCaseDisplayHelper.ApplyDetailDefaults(dto, expertScoped: expertId.HasValue);
            return dto;
        }
        public async Task<CreateMedicalCaseResponseDTO> CreateMedicalCaseAsync(CreateMedicalCaseRequestDTO dto)
        {
            var categoryname = await _unitOfWork.CategoryRepository.GetByIdAsync(dto.CategoryId ?? Guid.Empty);

            User? expert = null;

            if (dto.CreatedByExpertId.HasValue)
            {
                expert = await _unitOfWork.UserRepository.GetByIdAsync(dto.CreatedByExpertId.Value);
            }

            var medicalCase = new MedicalCase
            {
                Id = Guid.NewGuid(),
                CreatedByExpertId = dto.CreatedByExpertId,
                Title = dto.Title,
                Description = dto.Description,
                Difficulty = MedicalCaseDifficultyNormalizer.Normalize(dto.Difficulty),
                CategoryId = dto.CategoryId,
                SuggestedDiagnosis = dto.SuggestedDiagnosis,
                KeyFindings = dto.KeyFindings,
                ReflectiveQuestions = dto.ReflectiveQuestions,
                ValidatedByUserId = dto.CreatedByExpertId,
                ValidatedAt = dto.CreatedByExpertId.HasValue ? DateTime.UtcNow : null,
                IsApproved = dto.IsApproved ?? true,
                IsActive = dto.IsActive ?? true,
                CreatedAt = DateTime.UtcNow,
                IndexingStatus = DocumentIndexingStatuses.Pending,
                Version = SemanticDocumentVersion.Initial
            };

            await _unitOfWork.MedicalCaseRepository.AddAsync(medicalCase);
            await _unitOfWork.SaveAsync();

            return new CreateMedicalCaseResponseDTO
            {
                Id = medicalCase.Id,
                CreatedByExpertId = medicalCase.CreatedByExpertId,
                CaseOrigin = ExpertCaseOriginValues.ExpertCreated,
                ExpertName = expert?.FullName,
                Title = medicalCase.Title,
                Description = medicalCase.Description,
                Difficulty = medicalCase.Difficulty,
                CategoryName = categoryname?.Name,
                IsApproved = medicalCase.IsApproved,
                IsActive = medicalCase.IsActive,
                SuggestedDiagnosis = medicalCase.SuggestedDiagnosis,
                KeyFindings = medicalCase.KeyFindings,
                CreatedAt = medicalCase.CreatedAt,
            };
        }

        public async Task<CreateMedicalCaseResponseDTO> CreateMedicalCaseWithImagesJsonAsync(
            CreateExpertMedicalCaseJsonRequest request,
            Guid expertUserId,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);

            var dto = new CreateMedicalCaseRequestDTO
            {
                Title = request.Title,
                Description = request.Description,
                Difficulty = request.Difficulty,
                CategoryId = request.CategoryId,
                SuggestedDiagnosis = request.SuggestedDiagnosis,
                KeyFindings = request.KeyFindings,
                ReflectiveQuestions = request.ReflectiveQuestions,
                CreatedByExpertId = expertUserId
            };

            var created = await CreateMedicalCaseAsync(dto);
            var caseId = created.Id;

            foreach (var img in request.MedicalImages ?? Enumerable.Empty<CreateExpertMedicalCaseImageJson>())
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (string.IsNullOrWhiteSpace(img.ImageUrl))
                    continue;

                var image = new MedicalImage
                {
                    Id = Guid.NewGuid(),
                    CaseId = caseId,
                    ImageUrl = img.ImageUrl.Trim(),
                    Modality = MedicalImageModalityNormalizer.Normalize(img.Modality),
                    CreatedAt = DateTime.UtcNow
                };
                await _unitOfWork.MedicalImageRepository.AddAsync(image);

                foreach (var ann in img.Annotations ?? Enumerable.Empty<CreateAnnotationDTO>())
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await _unitOfWork.CaseAnnotationRepository.AddAsync(new CaseAnnotation
                    {
                        Id = Guid.NewGuid(),
                        ImageId = image.Id,
                        Label = ResolveAnnotationLabel(ann.Label),
                        Coordinates = ann.Coordinates,
                        CreatedAt = DateTime.UtcNow
                    });
                }
            }

            await _unitOfWork.SaveAsync();

            await ApplyCaseTagIdsAsync(caseId, request.TagIds, cancellationToken);
            await ApplyCaseOntologyAsync(
                caseId,
                request.AnatomySite,
                request.PathologyGroup,
                request.Modality,
                request.Difficulty ?? created.Difficulty,
                request.TagIds,
                cancellationToken);
            await EnsureDefaultLocationLesionTagsAsync(caseId, request.CategoryId, request.TagIds, cancellationToken);

            return created;
        }

        private async Task ApplyCaseOntologyAsync(
            Guid caseId,
            string? anatomySite,
            string? pathologyGroup,
            string? modality,
            string? difficulty,
            IEnumerable<Guid>? requestedTagIds,
            CancellationToken cancellationToken)
        {
            var normalizedAnatomy = NormalizeOntologyValue(anatomySite, MedicalOntologyValidation.AnatomySites, ExpertMedicalCaseDisplayHelper.DefaultAnatomySite);
            var normalizedPathology = NormalizeOntologyValue(pathologyGroup, MedicalOntologyValidation.PathologyGroups, ExpertMedicalCaseDisplayHelper.DefaultPathologyGroup);
            var normalizedModality = string.IsNullOrWhiteSpace(modality)
                ? "X-Ray"
                : MedicalImageModalityNormalizer.Normalize(modality);
            var normalizedDifficulty = MedicalCaseDifficultyNormalizer.Normalize(difficulty);
            var now = DateTime.UtcNow;

            var metadata = await _unitOfWork.Context.CaseMetadata.FirstOrDefaultAsync(m => m.CaseId == caseId, cancellationToken);
            if (metadata == null)
            {
                metadata = new CaseMetadata
                {
                    CaseId = caseId,
                    CreatedAt = now
                };
                await _unitOfWork.Context.CaseMetadata.AddAsync(metadata, cancellationToken);
            }

            metadata.Modality = normalizedModality;
            metadata.Anatomy = normalizedAnatomy;
            metadata.AnatomySite = normalizedAnatomy;
            metadata.PathologyGroup = normalizedPathology;
            metadata.Difficulty = normalizedDifficulty;
            metadata.SourceType = "Clinical";
            metadata.QualityScore ??= 0.85d;

            var existingTagIds = requestedTagIds?.ToHashSet() ?? new HashSet<Guid>();
            var locationTagId = await GetOrCreateTagIdByNameAndTypeAsync(normalizedAnatomy, "Location", now);
            if (!existingTagIds.Contains(locationTagId))
                await ApplyCaseTagIdsAsync(caseId, new[] { locationTagId }, cancellationToken);

            var lesionTagId = await GetOrCreateTagIdByNameAndTypeAsync(normalizedPathology, "Lesion Type", now);
            if (!existingTagIds.Contains(lesionTagId))
                await ApplyCaseTagIdsAsync(caseId, new[] { lesionTagId }, cancellationToken);

            await _unitOfWork.SaveAsync();
        }

        private static string NormalizeOntologyValue(string? raw, IReadOnlySet<string> allowed, string fallback)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return fallback;

            var trimmed = raw.Trim();
            return allowed.Contains(trimmed) ? trimmed : fallback;
        }

        private async Task EnsureDefaultLocationLesionTagsAsync(
            Guid caseId,
            Guid? categoryId,
            IEnumerable<Guid>? requestedTagIds,
            CancellationToken cancellationToken)
        {
            var existingTagIds = requestedTagIds?.ToHashSet() ?? new HashSet<Guid>();
            var caseTags = await _unitOfWork.CaseTagRepository
                .FindByCondition(ct => ct.CaseId == caseId)
                .Include(ct => ct.Tag)
                .ToListAsync(cancellationToken);

            var hasLocation = caseTags.Any(ct =>
                ct.Tag != null &&
                (string.Equals(ct.Tag.Type, "Location", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(ct.Tag.Type, "BoneLocation", StringComparison.OrdinalIgnoreCase)));
            var hasLesion = caseTags.Any(ct =>
                ct.Tag != null &&
                (string.Equals(ct.Tag.Type, "Lesion Type", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(ct.Tag.Type, "Lesion", StringComparison.OrdinalIgnoreCase)));

            if (hasLocation && hasLesion)
                return;

            var category = categoryId.HasValue
                ? await _unitOfWork.CategoryRepository.GetByIdAsync(categoryId.Value)
                : null;
            var categoryName = category?.Name?.Trim();
            if (string.IsNullOrWhiteSpace(categoryName))
                return;

            var now = DateTime.UtcNow;
            if (!hasLocation)
            {
                var locationTagId = await GetOrCreateTagIdByNameAndTypeAsync(categoryName, "Location", now);
                if (!existingTagIds.Contains(locationTagId))
                    await ApplyCaseTagIdsAsync(caseId, new[] { locationTagId }, cancellationToken);
            }

            if (!hasLesion)
            {
                var lesionTagId = await GetOrCreateTagIdByNameAndTypeAsync(categoryName, "Lesion Type", now);
                if (!existingTagIds.Contains(lesionTagId))
                    await ApplyCaseTagIdsAsync(caseId, new[] { lesionTagId }, cancellationToken);
            }
        }

        private async Task<Guid> GetOrCreateTagIdByNameAndTypeAsync(string name, string type, DateTime now)
        {
            var existing = await _unitOfWork.Context.Tags
                .FirstOrDefaultAsync(t => t.Name == name && t.Type == type);
            if (existing != null)
                return existing.Id;

            var tag = new Tag
            {
                Id = Guid.NewGuid(),
                Name = name,
                Type = type,
                CreatedAt = now,
                UpdatedAt = now
            };
            await _unitOfWork.Context.Tags.AddAsync(tag);
            await _unitOfWork.SaveAsync();
            return tag.Id;
        }

        private async Task ApplyCaseTagIdsAsync(Guid caseId, IEnumerable<Guid>? tagIds, CancellationToken cancellationToken)
        {
            if (tagIds == null)
                return;

            foreach (var tagId in tagIds.Distinct())
            {
                cancellationToken.ThrowIfCancellationRequested();
                var tagExists = await _unitOfWork.TagRepository.ExistsAsync(t => t.Id == tagId);
                if (!tagExists)
                    continue;
                var exists = await _unitOfWork.CaseTagRepository.ExistsAsync(x => x.CaseId == caseId && x.TagId == tagId);
                if (exists)
                    continue;
                await _unitOfWork.CaseTagRepository.AddAsync(new CaseTag
                {
                    CaseId = caseId,
                    TagId = tagId,
                    CreatedAt = DateTime.UtcNow
                });
            }

            await _unitOfWork.SaveAsync();
        }

        public async Task<UpdateMedicalCaseResponseDTO?> UpdateMedicalCaseAsync(
            Guid id,
            UpdateMedicalCaseDTORequest request,
            Guid? ownerExpertId = null)
        {
            var medicalCase = await _unitOfWork.MedicalCaseRepository.GetByIdAsync(id);

            if (medicalCase == null)
                return null;

            if (ownerExpertId.HasValue)
            {
                if (medicalCase.CreatedByExpertId != ownerExpertId)
                    return null;
                request.CreatedByExpertId = ownerExpertId;
                request.IsApproved = true;
                request.IsActive = true;
            }

            var contentChanged =
                !string.Equals(medicalCase.Title, request.Title, StringComparison.Ordinal) ||
                !string.Equals(medicalCase.Description, request.Description, StringComparison.Ordinal) ||
                !string.Equals(medicalCase.SuggestedDiagnosis, request.SuggestedDiagnosis, StringComparison.Ordinal) ||
                !string.Equals(medicalCase.KeyFindings, request.KeyFindings, StringComparison.Ordinal);
            var normalizedDifficulty = MedicalCaseDifficultyNormalizer.Normalize(request.Difficulty);
            var metadataChanged =
                !string.Equals(medicalCase.Difficulty, normalizedDifficulty, StringComparison.Ordinal) ||
                medicalCase.CategoryId != request.CategoryId ||
                medicalCase.IsApproved != request.IsApproved ||
                medicalCase.IsActive != request.IsActive ||
                medicalCase.CreatedByExpertId != request.CreatedByExpertId;

            // Update fields
            medicalCase.Title = request.Title;
            medicalCase.Description = request.Description;
            medicalCase.Difficulty = normalizedDifficulty;
            medicalCase.CategoryId = request.CategoryId;
            medicalCase.IsApproved = request.IsApproved;
            medicalCase.IsActive = request.IsActive;
            medicalCase.SuggestedDiagnosis = request.SuggestedDiagnosis;
            medicalCase.KeyFindings = request.KeyFindings;
            medicalCase.ReflectiveQuestions = request.ReflectiveQuestions ?? medicalCase.ReflectiveQuestions;
            medicalCase.CreatedByExpertId = request.CreatedByExpertId;
            medicalCase.UpdatedAt = DateTime.UtcNow;
            if (contentChanged)
            {
                // Trigger background re-indexing whenever embedding source text changes.
                medicalCase.IndexingStatus = DocumentIndexingStatuses.Pending;
                medicalCase.Version = BumpVersion(medicalCase.Version, isReindexing: true);
            }
            else if (metadataChanged)
            {
                // Minor metadata edits that do not require re-indexing still advance patch for traceability.
                medicalCase.Version = BumpVersion(medicalCase.Version, isReindexing: false);
            }

            _unitOfWork.MedicalCaseRepository.Update(medicalCase);
            await _unitOfWork.SaveAsync();

            if (!string.IsNullOrWhiteSpace(request.AnatomySite)
                || !string.IsNullOrWhiteSpace(request.PathologyGroup)
                || !string.IsNullOrWhiteSpace(request.Modality))
            {
                await ApplyCaseOntologyAsync(
                    medicalCase.Id,
                    request.AnatomySite,
                    request.PathologyGroup,
                    request.Modality,
                    medicalCase.Difficulty,
                    request.TagIds,
                    CancellationToken.None);
            }

            if (request.TagIds != null)
                await ApplyCaseTagIdsAsync(medicalCase.Id, request.TagIds, CancellationToken.None);

            // load related data
            var expert = await _unitOfWork.UserRepository
                .GetByIdAsync(medicalCase.CreatedByExpertId ?? Guid.Empty);

            var category = await _unitOfWork.CategoryRepository
                .GetByIdAsync(medicalCase.CategoryId ?? Guid.Empty);

            return new UpdateMedicalCaseResponseDTO
            {
                Id = medicalCase.Id,
                ExpertName = expert?.FullName,
                Title = medicalCase.Title,
                Description = medicalCase.Description,
                Difficulty = medicalCase.Difficulty,
                CategoryName = category?.Name,
                IsApproved = medicalCase.IsApproved,
                IsActive = medicalCase.IsActive,
                SuggestedDiagnosis = medicalCase.SuggestedDiagnosis,
                KeyFindings = medicalCase.KeyFindings,
                UpdatedAt = medicalCase.UpdatedAt
            };
        }

        /// <summary>DB <c>case_annotations.label</c> is NOT NULL; FE may omit label — store a neutral default.</summary>
        private static string ResolveAnnotationLabel(string? label) =>
            string.IsNullOrWhiteSpace(label) ? "finding" : label.Trim();

        private static string BumpVersion(string? currentVersion, bool isReindexing)
        {
            var normalized = SemanticDocumentVersion.Normalize(currentVersion);
            var match = SemanticVersionRegex.Match(normalized);
            if (!match.Success)
                return SemanticDocumentVersion.Initial;

            var major = int.Parse(match.Groups[1].Value);
            var minor = int.Parse(match.Groups[2].Value);
            var patch = int.Parse(match.Groups[3].Value);

            if (isReindexing)
                return $"{major}.{minor + 1}.0";

            return $"{major}.{minor}.{patch + 1}";
        }
        public async Task<bool> DeleteMedicalCaseAsync(Guid id, Guid? ownerExpertId = null)
        {
            var medicalCase = await _unitOfWork.MedicalCaseRepository
                .GetByIdAsync(id);

            if (medicalCase == null)
                return false;

            if (ownerExpertId.HasValue && medicalCase.CreatedByExpertId != ownerExpertId)
                return false;

            _ = _unitOfWork.MedicalCaseRepository.RemoveAsync(medicalCase);

            await _unitOfWork.SaveAsync();

            return true;
        }

        // Get all images for case
        public async Task<PagedResult<GetAllImageDTO>> GetAllImageAsync(int pageIndex, int pageSize)
        {
            var query = _unitOfWork.MedicalImageRepository.GetQueryable();

            var totalCount = await query.CountAsync();

            var images = await query
                .OrderByDescending(x => x.CreatedAt)
                .Skip((pageIndex - 1) * pageSize)
                .Take(pageSize)
              .Select(x => new GetAllImageDTO
              {
                  Id = x.Id,
                  CaseId = x.CaseId,
                  ImageUrl = x.ImageUrl,
                  FileName = Path.GetFileName(x.ImageUrl)
              })
                .ToListAsync();

            return new PagedResult<GetAllImageDTO>
            {
                Items = images,
                TotalCount = totalCount,
                PageIndex = pageIndex,
                PageSize = pageSize
            };
        }
    
        // Add image for case
        public async Task<AddMedicalImageDTO> AddImageAsync(AddMedicalImageDTOResponse dto)
        {
            var medicalCase = await _unitOfWork.MedicalCaseRepository.GetByIdAsync(dto.CaseId)
                ?? throw new KeyNotFoundException("Medical case not found.");

            var imageUrl = await SaveImageAsync(dto.Image, dto.CaseId);

            var image = new MedicalImage
            {
                Id = Guid.NewGuid(),
                CaseId = dto.CaseId,
                ImageUrl = imageUrl,
                Modality = MedicalImageModalityNormalizer.Normalize(dto.Modality),
                CreatedAt = DateTime.UtcNow
            };

            await _unitOfWork.MedicalImageRepository.AddAsync(image);
            await _unitOfWork.SaveAsync();

            return new AddMedicalImageDTO
            {
                Id = image.Id,
                ImageUrl = image.ImageUrl,
                Modality = image.Modality,
                CaseTitle = medicalCase.Title,
                Annotations = new List<AddAnnotationDTO>()
            };
        }

        // Xóa medical image
        public async Task<bool> DeleteMedicalImageAsync(Guid imageId)
        {
            var image = await _unitOfWork.MedicalImageRepository.GetByIdAsync(imageId);
            if (image == null) return false;

            // Extract object path from URL and delete from Supabase
            if (!string.IsNullOrWhiteSpace(image.ImageUrl))
            {
                try
                {
                    var objectPath = ExtractObjectPathFromUrl(image.ImageUrl, MedicalImagesBucket);
                    if (!string.IsNullOrEmpty(objectPath))
                    {
                        await _storageService.DeleteFileAsync(MedicalImagesBucket, objectPath);
                    }
                }
                catch
                {
                    // Log error but don't fail the deletion
                }
            }

            await _unitOfWork.MedicalImageRepository.DeleteAsync(imageId);
            await _unitOfWork.SaveAsync();
            return true;
        }

        private static string? ExtractObjectPathFromUrl(string url, string bucket)
        {
            if (string.IsNullOrWhiteSpace(url)) return null;

            try
            {
                var uri = new Uri(url);
                var path = uri.AbsolutePath;

                // Expected format: /storage/v1/object/public/{bucket}/{objectPath}
                var marker = $"/storage/v1/object/public/{bucket}/";
                var idx = path.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
                if (idx >= 0)
                {
                    return path[(idx + marker.Length)..];
                }

                return null;
            }
            catch
            {
                return null;
            }
        }

        // Get all annotations for image
        public async Task<PagedResult<GetAllAnnotationDTO>> GetAllAnnotationAsync(int pageIndex, int pageSize)
        {
            var request = _httpContextAccessor.HttpContext.Request;

            var baseUrl = $"{request.Scheme}://{request.Host}";

            var query = _unitOfWork.CaseAnnotationRepository
                .GetQueryable();

            var totalCount = await query.CountAsync();

            var annotations = await query
                .OrderByDescending(x => x.Id)
                .Skip((pageIndex - 1) * pageSize)
                .Take(pageSize)
                .Select(x => new GetAllAnnotationDTO
                {
                    Id = x.Id,
                    ImageId = x.ImageId,
                    ImageUrl = baseUrl + x.Image.ImageUrl,
                    Label = x.Label,
                    Coordinates = x.Coordinates
                })
                .ToListAsync();

            return new PagedResult<GetAllAnnotationDTO>
            {
                Items = annotations,
                TotalCount = totalCount,
                PageIndex = pageIndex,
                PageSize = pageSize
            };
        }

        // Add annotation for image
        public async Task<AddAnnotationDTO> AddAnnotationAsync(AddAnnotationDTOResponse dto)
        {
            var image = await _unitOfWork.MedicalImageRepository.GetByIdAsync(dto.ImageId)
                ?? throw new KeyNotFoundException("Image not found.");

            var annotation = new CaseAnnotation
            {
                Id = Guid.NewGuid(),
                ImageId = dto.ImageId,
                Label = ResolveAnnotationLabel(dto.Label),
                Coordinates = dto.Coordinates,
                CreatedAt = DateTime.UtcNow
            };

            await _unitOfWork.CaseAnnotationRepository.AddAsync(annotation);
            await _unitOfWork.SaveAsync();

            return new AddAnnotationDTO
            {
                Id = annotation.Id,
                Label = annotation.Label,
                Coordinates = annotation.Coordinates
            };
        }
  
        public async Task<PagedResult<GetCategoryDTO>> GetAllCategoryAsync(int pageIndex, int pageSize)
        {
            var query = _unitOfWork.CategoryRepository.GetQueryable();

            var totalCount = await query.CountAsync();

            var categories = await query
                .OrderBy(x => x.Name)
                .Skip((pageIndex - 1) * pageSize)
                .Take(pageSize)
                .Select(x => new GetCategoryDTO
                {
                    Id = x.Id,
                    Name = x.Name
                })
                .ToListAsync();

            return new PagedResult<GetCategoryDTO>
            {
                Items = categories,
                TotalCount = totalCount,
                PageIndex = pageIndex,
                PageSize = pageSize
            };
        }
    }       
}
