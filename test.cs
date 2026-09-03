using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using POS.Core.Data;
using POS.Core.Models;
using POS.Core.Repositories;
using POS.Core.Configuration;
using Microsoft.Extensions.DependencyInjection;

public class Test
{
    public static async Task Run()
    {
        var services = new ServiceCollection();
        // Register standard stuff or manually construct DbContext
        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseSqlite("Data Source=C:/Users/Sanjeewa/Dev/Projects(Personal)/POS/POS.BackOffice.UI/bin/Debug/net8.0-windows/POS.sqlite");
        
        using var context = new AppDbContext(optionsBuilder.Options);
        
        try 
        {
            var cat = await context.Categories.FirstOrDefaultAsync();
            if (cat == null) 
            {
                cat = new Category { CategoryCode = "TEST-01", CategoryName = "Test" };
                context.Categories.Add(cat);
                await context.SaveChangesAsync();
            }

            string docType = $"SUBCAT-{cat.Id}";
            var seq = new DocumentSequence 
            { 
                DocumentType = docType, 
                Prefix = "TEST-01-", 
                NextSequenceNumber = 1 
            };
            context.DocumentSequences.Add(seq);
            
            var subcat = new SubCategory 
            {
                CategoryId = cat.Id,
                SubCategoryCode = "TEST-01-001",
                SubCategoryName = "Test Sub"
            };
            context.SubCategories.Add(subcat);
            
            await context.SaveChangesAsync();
            Console.WriteLine("Saved successfully");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Exception: {ex.Message}");
            if (ex.InnerException != null) 
            {
                Console.WriteLine($"Inner Exception: {ex.InnerException.Message}");
            }
        }
    }
}
