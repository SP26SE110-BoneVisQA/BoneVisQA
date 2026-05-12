using BoneVisQA.Repositories.Models;
using BoneVisQA.Repositories.UnitOfWork;
using BoneVisQA.Services.Helpers;
using BoneVisQA.Services.Interfaces.Expert;
using BoneVisQA.Services.Models.Expert;
using BoneVisQA.Services.Services;
using BoneVisQA.Services.Models.Student;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
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

        public MedicalCaseService(
            IUnitOfWork unitOfWork,
            IWebHostEnvironment env,
            IHttpContextAccessor httpContextAccessor)
        {
            _unitOfWork = unitOfWork;
            _env = env;
            _httpContextAccessor = httpContextAccessor;
        }
        private async Task<string> SaveImageAsync(IFormFile file)
        {
            var uploadFolder = Path.Combine(_env.ContentRootPath, "uploads", "images");
            if (!Directory.Exists(uploadFolder))
                Directory.CreateDirectory(uploadFolder);

            var extension = Path.GetExtension(file.FileName);
            var originalName = Path.GetFileNameWithoutExtension(file.FileName);

            // Shorten name if too long
            if (originalName.Length > 50)
                originalName = originalName.Substring(0, 50);

            var fileName = $"{originalName}_{DateTime.UtcNow:yyyyMMddHHmmss}{extension}";
            var filePath = Path.Combine(uploadFolder, fileName);

            using var stream = new FileStream(filePath, FileMode.Create);
            await file.CopyToAsync(stream);

            var relativeUrl = $"/uploads/images/{fileName}";

            // Tạo absolute URL với backend base URL
            var request = _httpContextAccessor.HttpContext?.Request;
            if (request != null)
            {
                var baseUrl = $"{request.Scheme}://{request.Host.Host}:{request.Host.Port ?? 5046}";
                return $"{baseUrl}{relativeUrl}";
            }

            return relativeUrl;
        }
        public async Task<PagedResult<GetMedicalCaseDTO>> GetAllMedicalCasesAsync(int pageIndex, int pageSize)
        {
            var query = _unitOfWork.MedicalCaseRepository.GetQueryable().AsNoTracking();

            var totalCount = await query.CountAsync();

            var medicalCases = await query
                .OrderByDescending(x => x.CreatedAt)
                .Skip((pageIndex - 1) * pageSize)
                .Take(pageSize)
                .Select(x => new GetMedicalCaseDTO
                {
                    Id = x.Id,
                    Title = x.Title,
                    CreatedByExpertId = x.CreatedByExpertId,
                    ExpertName = x.CreatedByExpert != null ? x.CreatedByExpert.FullName : null,
                    Description = x.Description,
                    Difficulty = x.Difficulty,
                    CategoryId = x.CategoryId,
                    CategoryName = x.Category != null ? x.Category.Name : null,
                    BoneLocation = x.CaseTags
                        .Where(ct => ct.Tag != null &&
                            (ct.Tag.Type == "Location" || ct.Tag.Type == "BoneLocation"))
                        .Select(ct => ct.Tag!.Name)
                        .FirstOrDefault() ?? string.Empty,
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
                })
                .ToListAsync();

            foreach (var row in medicalCases)
                ExpertMedicalCaseDisplayHelper.ApplyListDefaults(row);

            return new PagedResult<GetMedicalCaseDTO>
            {
                Items = medicalCases,
                TotalCount = totalCount,
                PageIndex = pageIndex,
                PageSize = pageSize
            };
        }

        public async Task<GetExpertMedicalCaseDetailDto?> GetMedicalCaseByIdAsync(Guid id)
        {
            var entity = await _unitOfWork.MedicalCaseRepository.GetQueryable()
                .AsNoTracking()
                .Include(c => c.Category)
                .Include(c => c.CreatedByExpert)
                .Include(c => c.CaseTags)
                    .ThenInclude(ct => ct.Tag)
                .Include(c => c.MedicalImages)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (entity == null)
                return null;

            var boneLocation = ExpertMedicalCaseDisplayHelper.ResolveBoneLocationFromTags(entity.CaseTags);

            var dto = new GetExpertMedicalCaseDetailDto
            {
                Id = entity.Id,
                Title = entity.Title,
                CreatedByExpertId = entity.CreatedByExpertId,
                ExpertName = entity.CreatedByExpert?.FullName,
                Description = entity.Description,
                Difficulty = entity.Difficulty,
                CategoryId = entity.CategoryId,
                CategoryName = entity.Category?.Name,
                BoneLocation = boneLocation,
                IsApproved = entity.IsApproved,
                IsActive = entity.IsActive,
                SuggestedDiagnosis = entity.SuggestedDiagnosis,
                KeyFindings = entity.KeyFindings,
                CreatedAt = entity.CreatedAt,
                UpdatedAt = entity.UpdatedAt,
                MedicalImages = entity.MedicalImages
                    .OrderByDescending(m => m.CreatedAt)
                    .Select(m => new ExpertMedicalCaseImageSummaryDto
                    {
                        Id = m.Id,
                        ImageUrl = m.ImageUrl,
                        Modality = m.Modality,
                        CreatedAt = m.CreatedAt
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
                    ?? string.Empty
            };

            ExpertMedicalCaseDisplayHelper.ApplyDetailDefaults(dto);
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
                IsApproved = false,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                IndexingStatus = DocumentIndexingStatuses.Pending,
                Version = SemanticDocumentVersion.Initial
            };

            await _unitOfWork.MedicalCaseRepository.AddAsync(medicalCase);
            await _unitOfWork.SaveAsync();

            return new CreateMedicalCaseResponseDTO
            {
                Id = medicalCase.Id,
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

            return created;
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

        public async Task<UpdateMedicalCaseResponseDTO?> UpdateMedicalCaseAsync(Guid id, UpdateMedicalCaseDTORequest request)
        {
            var medicalCase = await _unitOfWork.MedicalCaseRepository.GetByIdAsync(id);

            if (medicalCase == null)
                return null;

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
        public async Task<bool> DeleteMedicalCaseAsync(Guid id)
        {
            var medicalCase = await _unitOfWork.MedicalCaseRepository
                .GetByIdAsync(id);

            if (medicalCase == null)
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

            var imageUrl = await SaveImageAsync(dto.Image);

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

            // TODO: Xóa file từ Supabase storage nếu cần

            await _unitOfWork.MedicalImageRepository.DeleteAsync(imageId);
            await _unitOfWork.SaveAsync();
            return true;
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
