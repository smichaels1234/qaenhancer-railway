using System;
using System.Collections.Generic;
using System.Linq;
using backend.Data;
using backend.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TestTableController : ControllerBase
    {
        private readonly QAEnhancerDbContext _context;
        private readonly ILogger<TestTableController> _logger;

        public TestTableController(QAEnhancerDbContext context, ILogger<TestTableController> logger)
        {
            _context = context;
            _logger = logger;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<TestTable>>> GetAll()
        {
            _logger.LogInformation("GetAll method called at {Time}", DateTime.UtcNow);
            
            try
            {
                _logger.LogInformation("Starting database query...");
                var startTime = DateTime.UtcNow;
                
                var items = await _context.TestTables.ToListAsync();
                
                var endTime = DateTime.UtcNow;
                var duration = endTime - startTime;
                
                _logger.LogInformation("Database query completed in {Duration}ms. Found {Count} items.", 
                    duration.TotalMilliseconds, items.Count());
                
                return Ok(items);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while fetching test table data");
                return StatusCode(500, "Internal server error");
            }
        }
    }
}