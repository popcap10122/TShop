﻿using eShopSolution.Data.EF;
using eShopSolution.Data.Entities;
using eShopSolution.Utilities;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Linq;
using eShopSolution.ViewModels.Common;
using Microsoft.AspNetCore.Http;
using System.Net.Http.Headers;
using System.IO;
using eShopSolution.Application.Common;
using eShopSolution.ViewModels.Catalog.ProductImages;
using eShopSolution.ViewModels.Catalog.Products;
using eShopSolution.Application.Catolog.ProductImages;
using eShopSolution.ViewModels.Catalog.Category;
using eShopSolution.Utilities.Constant;

namespace eShopSolution.Application.Catolog.Products
{
    public class ProductService : IProductService
    {
        private readonly EShopDbContext _context;
        private readonly IStrorageService _storageService;

        public ProductService(EShopDbContext context, IStrorageService storageService)
        {
            _context = context;
            _storageService = storageService;
        }

        public async Task AddViewCount(int productId)
        {
            var product = await _context.Products.FindAsync(productId);
            product.ViewCount += 1;
            await _context.SaveChangesAsync();
        }

        public async Task<int> Create(ProductCreateRequest request)
        {
            var languages = _context.Languages;
            var productTranslations = new List<ProductTranslation>();

            foreach (var language in languages)
            {
                if (language.Id == request.LanguageId)
                {
                    productTranslations.Add(new ProductTranslation()
                    {
                        Name = request.Name,
                        Description = request.Description,
                        Details = request.Details,
                        SeoTitle = request.SeoTitle,
                        SeoDescription = request.SeoDescription,
                        SeoAlias = request.SeoAlias,
                        LanguageId = request.LanguageId
                    });
                }
                else
                {
                    productTranslations.Add(new ProductTranslation()
                    {
                        Name = SystemConstant.ProductSettings.NA,
                        Description = SystemConstant.ProductSettings.NA,
                        SeoTitle = SystemConstant.ProductSettings.NA,
                        SeoDescription = SystemConstant.ProductSettings.NA,
                        LanguageId = language.Id
                    });
                }
            }
            var product = new Product()
            {
                Price = request.Price,
                Original = request.Original,
                Stock = request.Stock,
                ViewCount = 0,
                DateCreated = DateTime.Now,
                ProductTranslations = productTranslations
            };
            //Save image
            if (request.ThumbnailImage != null)
            {
                product.ProductImages = new List<ProductImage>()
                {
                    new ProductImage()
                    {
                        Caption = "Thumbnail Images",
                        DateCreated = DateTime.Now,
                        FileSize = request.ThumbnailImage.Length,
                        ImagePath =  await this.SaveFile(request.ThumbnailImage),
                        IsDefault = true,
                        SortOrder = 1
                    }
                };
            }

            _context.Products.Add(product);
            await _context.SaveChangesAsync();
            return product.Id;
        }

        public async Task<ProductViewModel> GetById(int productId, string languageId)
        {
            var product = await _context.Products.FindAsync(productId);
            var productTraslation = await _context.ProductTranslations.FirstOrDefaultAsync(x => x.ProductId == product.Id
            && x.LanguageId == languageId);
            var productImages = await _context.ProductImages.Where(x => x.ProductId == product.Id).ToListAsync();
            var otherProductImages = await _context.ProductImages.Where(x => x.ProductId == product.Id && x.IsDefault != true).Select(x => new ProductImageViewModel
            {
                Id = x.Id,
                ImagePath = x.ImagePath,
                Caption = x.Caption,
                SortOrder = x.SortOrder
            }).ToListAsync();
            var catgory = await (from c in _context.Categories
                                 join ct in _context.CategoryTranslations on c.Id equals ct.CategoryId
                                 join pic in _context.ProductInCategories on c.Id equals pic.CategoryId
                                 where pic.ProductId == productId && ct.LanguageId == languageId
                                 select ct.Name).ToListAsync();

            var productViewModel = new ProductViewModel()
            {
                Id = product.Id,
                Price = product.Price,
                Original = product.Original,
                Stock = product.Stock,
                ViewCount = product.ViewCount,
                DateCreated = product.DateCreated,
                Name = productTraslation != null ? productTraslation.Name : null,
                Description = productTraslation != null ? productTraslation.Description : null,
                Details = productTraslation != null ? productTraslation.Details : null,
                SeoDescription = productTraslation != null ? productTraslation.SeoDescription : null,
                SeoTitle = productTraslation != null ? productTraslation.SeoTitle : null,
                SeoAlias = productTraslation != null ? productTraslation.SeoAlias : null,
                LanguageId = productTraslation != null ? productTraslation.LanguageId : null,
                Categories = catgory,
                Image = productImages.Count != 0 ? productImages.Where(x => x.IsDefault == true).FirstOrDefault().ImagePath : "",
                OtherImages = otherProductImages
            };

            return productViewModel;
        }

        public async Task<int> Delete(int productId)
        {
            var product = await _context.Products.FindAsync(productId);
            if (product == null)
                throw new EShopException($"Cannot find product id {productId} ");
            var images = _context.ProductImages.Where(x => x.ProductId == productId);
            if (images != null)
            {
                foreach (var image in images)
                {
                    await _storageService.DeleteFileAsync(image.ImagePath);
                }
            }
            _context.Products.Remove(product);
            return await _context.SaveChangesAsync();
        }

        public async Task<ApiResult<PagedResult<ProductViewModel>>> GetAllPaging(GetManageProductPagingRequest request)
        {
            //1: Select
            var query = from p in _context.Products
                        join pt in _context.ProductTranslations on p.Id equals pt.ProductId
                        join pic in _context.ProductInCategories on p.Id equals pic.ProductId into pptpic
                        from pic in pptpic.DefaultIfEmpty()
                        join c in _context.Categories on pic.CategoryId equals c.Id into pc
                        from c in pc.DefaultIfEmpty()
                        join pi in _context.ProductImages on p.Id equals pi.ProductId into ppi
                        from pi in ppi.DefaultIfEmpty()
                        where pt.LanguageId == request.LanguageId
                        && pi.IsDefault == true
                        select new
                        {
                            p,
                            pt,
                            pic,
                            pi
                        }
                        ;
            //2: check filter
            if (!string.IsNullOrEmpty(request.Keyword))
            {
                query = query.Where(x => x.pt.Name.Contains(request.Keyword));
            }

            if (request.CategoryId != 0 && request.CategoryId != null)
            {
                query = query.Where(p => p.pic.CategoryId == request.CategoryId);
            }
            //3: paging
            int totalRow = await query.CountAsync();
            var data = await query.Skip((request.PageIndex - 1) * request.PageSize).Take(request.PageSize)
                .Select(x => new ProductViewModel()
                {
                    Id = x.p.Id,
                    Price = x.p.Price,
                    Original = x.p.Original,
                    Stock = x.p.Stock,
                    ViewCount = x.p.ViewCount,
                    DateCreated = x.p.DateCreated,
                    Name = x.pt.Name,
                    Description = x.pt.Description,
                    Details = x.pt.Details,
                    SeoDescription = x.pt.SeoDescription,
                    SeoTitle = x.pt.SeoTitle,
                    SeoAlias = x.pt.SeoAlias,
                    LanguageId = x.pt.LanguageId,
                    Image = x.pi.ImagePath
                }).ToListAsync()
                ;
            // select vs projection
            var PageResult = new PagedResult<ProductViewModel>()
            {
                PageSize = request.PageSize,
                PageIndex = request.PageIndex,
                TotalRecords = totalRow,
                Items = data
            };
            return new ApiSuccessResult<PagedResult<ProductViewModel>>(PageResult);
        }

        public async Task<int> Update(ProductUpdateRequest request)
        {
            var product = await _context.Products.FindAsync(request.Id);
            var productTranslation = await _context.ProductTranslations
                .FirstOrDefaultAsync(x => x.ProductId == request.Id && x.LanguageId == request.LanguageId);
            if (product == null || productTranslation == null)
            {
                throw new Exception($"Cannot find product with Id { request.Id } ");
            }
            productTranslation.Name = request.Name;
            productTranslation.Description = request.Description;
            productTranslation.SeoTitle = request.SeoTitle;
            productTranslation.SeoDescription = request.SeoDescription;
            productTranslation.SeoAlias = request.SeoAlias;
            productTranslation.Details = request.Details;

            //Save Images
            if (request.ThumbnailImage != null)
            {
                var thumbnailImage = await _context.ProductImages.FirstOrDefaultAsync(x => x.IsDefault == true && x.ProductId == request.Id);
                if (thumbnailImage != null)
                {
                    thumbnailImage.FileSize = request.ThumbnailImage.Length;
                    thumbnailImage.ImagePath = await this.SaveFile(request.ThumbnailImage);
                    _context.ProductImages.Update(thumbnailImage);
                }
                else
                {
                    product.ProductImages = new List<ProductImage>()
                    {
                        new ProductImage()
                        {
                            Caption = "Thumbnail Images",
                            DateCreated = DateTime.Now,
                            FileSize = request.ThumbnailImage.Length,
                            ImagePath =  await this.SaveFile(request.ThumbnailImage),
                            IsDefault = true,
                            SortOrder = 1
                        }
                    };
                }
            }

            return await _context.SaveChangesAsync();
        }

        public async Task<bool> UpdatePrice(int productId, decimal newPrice)
        {
            var product = await _context.Products.FindAsync(productId);

            if (product == null)
            {
                throw new Exception($"Cannot find product with Id { productId } ");
            }
            product.Price = newPrice;
            return await _context.SaveChangesAsync() > 0;
        }

        public async Task<bool> UpdateStock(int productId, int addedQuantity)
        {
            var product = await _context.Products.FindAsync(productId);

            if (product == null)
            {
                throw new Exception($"Cannot find product with Id { productId } ");
            }
            product.Stock += addedQuantity;
            return await _context.SaveChangesAsync() > 0;
        }

        public async Task<string> SaveFile(IFormFile file)
        {
            var orginalFileName = ContentDispositionHeaderValue.Parse(file.ContentDisposition).FileName.Trim('"');
            var fileName = $"{Guid.NewGuid()} {Path.GetExtension(orginalFileName)}";
            await _storageService.SaveFileAsync(file.OpenReadStream(), fileName);
            return "/" + SystemConstant.ProductSettings.USER_CONTENT_FOLDER_NAME + "/" + fileName;
        }

        public async Task<int> AddImage(int productId, ProductImageCreateRequest request)
        {
            var productImage = new ProductImage()
            {
                ProductId = productId,
                Caption = request.Caption,
                DateCreated = DateTime.Now,
                IsDefault = request.IsDefault,
                SortOrder = request.SortOrder
            };
            if (request.ImageFile != null)
            {
                productImage.FileSize = request.ImageFile.Length;
                productImage.ImagePath = await this.SaveFile(request.ImageFile);
            }
            _context.ProductImages.Add(productImage);
            await _context.SaveChangesAsync();
            return productImage.Id;
        }

        public async Task<int> UpdateImage(int imageId, ProductImageUpdateRequest request)
        {
            var productImage = await _context.ProductImages.FirstAsync(x => x.Id == imageId);
            if (productImage != null)
                throw new EShopException($"Cannot find productimage {imageId}");
            if (request.ImageFile != null)
            {
                productImage.FileSize = request.ImageFile.Length;
                productImage.ImagePath = await this.SaveFile(request.ImageFile);
                productImage.Caption = request.Caption;
                productImage.IsDefault = request.IsDefault;
                productImage.SortOrder = request.SortOrder;
            }
            _context.ProductImages.Update(productImage);
            return await _context.SaveChangesAsync();
        }

        public async Task<int> RemoveImage(int imageId)
        {
            var productImage = await _context.ProductImages.FirstAsync(x => x.Id == imageId);
            if (productImage != null)
                throw new EShopException($"Cannot find productimage {imageId}");
            _context.ProductImages.Remove(productImage);
            return await _context.SaveChangesAsync();
        }

        public async Task<List<ProductImageViewModel>> GetListImages(int productId)
        {
            return await _context.ProductImages.Where(x => x.ProductId == productId)
                .Select(i => new ProductImageViewModel()
                {
                    Id = i.Id,
                    Caption = i.Caption,
                    DateCreated = i.DateCreated,
                    ProductId = i.ProductId,
                    FileSize = i.FileSize,
                    ImagePath = i.ImagePath,
                    IsDefault = i.IsDefault,
                    SortOrder = i.SortOrder
                }).ToListAsync();
        }

        public async Task<ProductImageViewModel> GetImageById(int imageId)
        {
            var Image = await _context.ProductImages.FirstAsync(x => x.Id == imageId);
            if (Image != null)
                throw new EShopException($"Cannot find image with Id :  {imageId}");
            var data = new ProductImageViewModel()
            {
                Id = Image.Id,
                Caption = Image.Caption,
                DateCreated = Image.DateCreated,
                ProductId = Image.ProductId,
                FileSize = Image.FileSize,
                ImagePath = Image.ImagePath,
                IsDefault = Image.IsDefault,
                SortOrder = Image.SortOrder
            };
            return data;
        }

        public async Task<PagedResult<ProductViewModel>> GetAllByCategoryId(string languageId, GetPublicProductPagingRequest request)
        {
            var query = from p in _context.Products
                        join pt in _context.ProductTranslations on p.Id equals pt.ProductId
                        join pic in _context.ProductInCategories on p.Id equals pic.ProductId
                        join c in _context.Categories on pic.CategoryId equals c.Id
                        where pt.LanguageId == languageId
                        select (new { p, pt, pic });

            if (request.CategoryId.HasValue && request.CategoryId.Value > 0)
            {
                query = query.Where(x => x.pic.CategoryId == request.CategoryId);
            }

            var totalRow = await query.CountAsync();
            var data = await query.Skip((request.PageIndex - 1) * request.PageSize).Take(request.PageSize)
                .Select(x => new ProductViewModel()
                {
                    Id = x.p.Id,
                    Price = x.p.Price,
                    Original = x.p.Original,
                    Stock = x.p.Stock,
                    ViewCount = x.p.ViewCount,
                    DateCreated = x.p.DateCreated,
                    Name = x.pt.Name,
                    Description = x.pt.Description,
                    Details = x.pt.Details,
                    SeoDescription = x.pt.SeoDescription,
                    SeoTitle = x.pt.SeoTitle,
                    SeoAlias = x.pt.SeoAlias,
                    LanguageId = x.pt.LanguageId
                }).ToListAsync();
            var pagedResult = new PagedResult<ProductViewModel>
            {
                PageSize = request.PageSize,
                PageIndex = request.PageIndex,
                Items = data,
                TotalRecords = totalRow
            };
            return pagedResult;
        }

        public async Task<ApiResult<bool>> AssignCategory(int Id, CategoryAssignRequest request)
        {
            var product = await _context.Products.FindAsync(Id);
            if (product == null)
                return new ApiErrorResult<bool>("Sản phẩm không tồn tại");
            // remove category if doesnt check from request
            var categoriesList = request.Categories.ToList();

            foreach (var category in categoriesList)
            {
                var productInCategory = await _context.ProductInCategories.FirstOrDefaultAsync(x => x.ProductId == Id && x.CategoryId == int.Parse(category.Id));
                if (productInCategory != null && category.Selected == false)
                {
                    _context.ProductInCategories.Remove(productInCategory);
                }
                else if (productInCategory == null && category.Selected)
                {
                    _context.ProductInCategories.Add(new ProductInCategory()
                    {
                        ProductId = Id,
                        CategoryId = int.Parse(category.Id)
                    });
                }
            }

            await _context.SaveChangesAsync();
            return new ApiSuccessResult<bool>();
        }

        public async Task<List<ProductViewModel>> GetFeaturedProducts(string languageId, int take)
        {
            var query = from p in _context.Products
                        join pt in _context.ProductTranslations on p.Id equals pt.ProductId
                        join pic in _context.ProductInCategories on p.Id equals pic.ProductId into pptpic
                        from pic in pptpic.DefaultIfEmpty()
                        join pi in _context.ProductImages.Where(x => x.IsDefault == true) on p.Id equals pi.ProductId into ppi
                        from pi in ppi.DefaultIfEmpty()
                        join c in _context.Categories on pic.CategoryId equals c.Id into pc
                        from c in pc.DefaultIfEmpty()
                        where pt.LanguageId == languageId && (pi.IsDefault == true || pi == null)
                        && p.IsFeatured == true
                        select new
                        {
                            p,
                            pt,
                            pic,
                            pi
                        }
                       ;
            var data = await query.OrderByDescending(x => x.p.DateCreated).Take(take)
                .Select(x => new ProductViewModel()
                {
                    Id = x.p.Id,
                    Price = x.p.Price,
                    Original = x.p.Original,
                    Stock = x.p.Stock,
                    ViewCount = x.p.ViewCount,
                    DateCreated = x.p.DateCreated,
                    Name = x.pt.Name,
                    Description = x.pt.Description,
                    Details = x.pt.Details,
                    SeoDescription = x.pt.SeoDescription,
                    SeoTitle = x.pt.SeoTitle,
                    SeoAlias = x.pt.SeoAlias,
                    LanguageId = x.pt.LanguageId,
                    Image = x.pi.ImagePath
                }).ToListAsync()
                ;
            // select vs projection

            return data;
        }

        public async Task<List<ProductViewModel>> GetLatestProducts(string languageId, int take)
        {
            var query = from p in _context.Products
                        join pt in _context.ProductTranslations on p.Id equals pt.ProductId
                        join pic in _context.ProductInCategories on p.Id equals pic.ProductId into pptpic
                        from pic in pptpic.DefaultIfEmpty()
                        join pi in _context.ProductImages.Where(x => x.IsDefault == true) on p.Id equals pi.ProductId into ppi
                        from pi in ppi.DefaultIfEmpty()
                        join c in _context.Categories on pic.CategoryId equals c.Id into pc
                        from c in pc.DefaultIfEmpty()
                        where pt.LanguageId == languageId && (pi.IsDefault == true || pi == null)
                        && p.IsFeatured == true
                        select new
                        {
                            p,
                            pt,
                            pic,
                            pi
                        }
                       ;
            var data = await query.OrderByDescending(x => x.p.DateCreated).Take(take)
                .Select(x => new ProductViewModel()
                {
                    Id = x.p.Id,
                    Price = x.p.Price,
                    Original = x.p.Original,
                    Stock = x.p.Stock,
                    ViewCount = x.p.ViewCount,
                    DateCreated = x.p.DateCreated,
                    Name = x.pt.Name,
                    Description = x.pt.Description,
                    Details = x.pt.Details,
                    SeoDescription = x.pt.SeoDescription,
                    SeoTitle = x.pt.SeoTitle,
                    SeoAlias = x.pt.SeoAlias,
                    LanguageId = x.pt.LanguageId,
                    Image = x.pi.ImagePath
                }).ToListAsync()
                ;
            // select vs projection

            return data;
        }
    }
}