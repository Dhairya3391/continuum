# Background Jobs Documentation

## Overview
The Personal Universe Simulator uses Hangfire for background job processing, enabling scheduled tasks, recurring jobs, and asynchronous operations. Background jobs handle simulation ticks, particle decay, cleanup operations, and other time-based processes.

**Job Scheduler:** Hangfire  
**Storage:** SQL Server (same database)  
**Dashboard:** Available at `/hangfire`  
**Execution:** Multi-threaded worker pool

---

## Architecture

```
┌─────────────────────────┐
│  Hangfire Dashboard     │
│  (Web UI)               │
│  http://localhost:5004  │
│  /hangfire              │
└───────────┬─────────────┘
            │
            │ Monitor/Manage
            ▼
┌─────────────────────────┐
│   Hangfire Server       │
│   (Background Processor)│
│                         │
│  ┌───────────────────┐  │
│  │  Job Queue        │  │
│  │  - Enqueued       │  │
│  │  - Processing     │  │
│  │  - Scheduled      │  │
│  │  - Recurring      │  │
│  └───────────────────┘  │
│                         │
│  ┌───────────────────┐  │
│  │  Worker Threads   │  │
│  │  (Parallel exec)  │  │
│  └───────────────────┘  │
└───────────┬─────────────┘
            │
            │ Execute
            ▼
┌─────────────────────────┐
│   Simulation Jobs       │
│   - Daily Tick          │
│   - Particle Decay      │
│   - Cleanup             │
└─────────────────────────┘
            │
            │ Persist
            ▼
┌─────────────────────────┐
│   SQL Server            │
│   - Hangfire tables     │
│   - Application data    │
└─────────────────────────┘
```

---

## Hangfire Configuration

### Registration (Program.cs)

```csharp
// Add Hangfire services
builder.Services.AddHangfire(config =>
{
    config
        .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
        .UseSimpleAssemblyNameTypeSerializer()
        .UseRecommendedSerializerSettings()
        .UseSqlServerStorage(
            builder.Configuration.GetConnectionString("DefaultConnection"),
            new SqlServerStorageOptions
            {
                CommandBatchMaxTimeout = TimeSpan.FromMinutes(5),
                SlidingInvisibilityTimeout = TimeSpan.FromMinutes(5),
                QueuePollInterval = TimeSpan.Zero,
                UseRecommendedIsolationLevel = true,
                DisableGlobalLocks = true,
                SchemaName = "HangFire" // Separate schema for Hangfire tables
            });
});

// Add Hangfire server
builder.Services.AddHangfireServer(options =>
{
    options.WorkerCount = Environment.ProcessorCount * 2; // 2 workers per CPU core
    options.ServerName = $"SimulationEngine-{Environment.MachineName}";
    options.Queues = new[] { "default", "simulation", "maintenance" };
});

// Register job classes
builder.Services.AddScoped<SimulationJobs>();

var app = builder.Build();

// Enable Hangfire dashboard
app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = new[] { new HangfireAuthorizationFilter() },
    DashboardTitle = "Personal Universe - Background Jobs"
});
```

### Authorization Filter

```csharp
public class HangfireAuthorizationFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        // In production, add proper authentication
        #if DEBUG
            return true; // Allow all in development
        #else
            // Check if user is authenticated and has admin role
            var httpContext = context.GetHttpContext();
            return httpContext.User.Identity?.IsAuthenticated == true &&
                   httpContext.User.IsInRole("Admin");
        #endif
    }
}
```

---

## Job Types

### 1. Recurring Jobs

**Execute on schedule** - Cron expressions define frequency.

**Daily Simulation Tick:**
```csharp
RecurringJob.AddOrUpdate<SimulationJobs>(
    "daily-simulation-tick",
    job => job.ProcessDailyTickAsync(),
    Cron.Daily(0, 0), // Every day at midnight UTC
    new RecurringJobOptions
    {
        TimeZone = TimeZoneInfo.Utc,
        QueueName = "simulation"
    }
);
```

**Hourly Decay Check:**
```csharp
RecurringJob.AddOrUpdate<SimulationJobs>(
    "hourly-particle-decay",
    job => job.ProcessParticleDecayAsync(),
    Cron.Hourly(), // Every hour
    new RecurringJobOptions
    {
        TimeZone = TimeZoneInfo.Utc,
        QueueName = "simulation"
    }
);
```

**Weekly Cleanup:**
```csharp
RecurringJob.AddOrUpdate<SimulationJobs>(
    "weekly-cleanup",
    job => job.CleanupExpiredParticlesAsync(),
    Cron.Weekly(DayOfWeek.Sunday, 2, 0), // Sundays at 2 AM UTC
    new RecurringJobOptions
    {
        TimeZone = TimeZoneInfo.Utc,
        QueueName = "maintenance"
    }
);
```

---

### 2. Fire-and-Forget Jobs

**Execute once immediately** - Enqueued and processed ASAP.

```csharp
BackgroundJob.Enqueue<SimulationJobs>(job => 
    job.ProcessDailyTickAsync()
);
```

**Use Cases:**
- Manual tick trigger via API
- Event-driven processing
- User-initiated operations

---

### 3. Delayed Jobs

**Execute after delay** - Schedule for future execution.

```csharp
BackgroundJob.Schedule<SimulationJobs>(
    job => job.ProcessDailyTickAsync(),
    TimeSpan.FromMinutes(30)
);
```

**Use Cases:**
- Retry failed operations
- Deferred processing
- Rate limiting

---

### 4. Continuation Jobs

**Execute after another job** - Chain dependent jobs.

```csharp
var parentJobId = BackgroundJob.Enqueue<SimulationJobs>(
    job => job.ProcessDailyTickAsync()
);

BackgroundJob.ContinueJobWith<SimulationJobs>(
    parentJobId,
    job => job.CleanupExpiredParticlesAsync()
);
```

---

## Simulation Jobs

### 1. Daily Simulation Tick

**Schedule:** Daily at midnight UTC  
**Queue:** simulation  
**Duration:** ~5-30 seconds (depends on particle count)

**Purpose:**
- Update all particle positions
- Calculate interactions (merge/repel/attract)
- Apply physics rules (velocity, acceleration)
- Broadcast universe state

**Implementation:**
```csharp
public async Task ProcessDailyTickAsync()
{
    try
    {
        _logger.LogInformation("Starting daily simulation tick at {Time}", DateTime.UtcNow);
        
        await _simulationService.ProcessSimulationTickAsync();
        
        _logger.LogInformation("Completed daily simulation tick at {Time}", DateTime.UtcNow);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error during daily simulation tick");
        throw; // Hangfire will retry
    }
}
```

**What Happens During Tick:**
1. Get all active particles from database
2. Update positions based on velocity
3. Apply toroidal wrapping (universe boundaries)
4. Find neighbors within interaction radius
5. Calculate compatibility between neighbors
6. Process interactions:
   - Merge if compatibility > 0.7
   - Repel if compatibility < 0.3
   - Attract if 0.3 ≤ compatibility ≤ 0.7
7. Update particle states in database
8. Save universe state snapshot
9. Publish events via RabbitMQ
10. Broadcast to SignalR clients

---

### 2. Particle Decay Processing

**Schedule:** Hourly  
**Queue:** simulation  
**Duration:** ~1-5 seconds

**Purpose:**
- Check particles for inactivity (no daily input)
- Increment decay level
- Expire particles at 100% decay

**Implementation:**
```csharp
public async Task ProcessParticleDecayAsync()
{
    try
    {
        _logger.LogInformation("Starting particle decay processing at {Time}", DateTime.UtcNow);
        
        var activeParticles = await _particleRepository.GetActiveParticlesAsync();
        var decayThreshold = DateTime.UtcNow.AddHours(-24);
        var decayedCount = 0;

        foreach (var particle in activeParticles)
        {
            // Check if particle hasn't received input in 24 hours
            if (particle.LastInputAt < decayThreshold && particle.State == ParticleState.Active)
            {
                particle.State = ParticleState.Decaying;
                particle.DecayLevel += 10; // 10% decay per hour

                if (particle.DecayLevel >= 100)
                {
                    particle.State = ParticleState.Expired;
                    
                    // Publish expiration event
                    await _eventPublisher.PublishAsync("particle.expired", new ParticleExpiredEvent(
                        EventId: Guid.NewGuid(),
                        Timestamp: DateTime.UtcNow,
                        ParticleId: particle.Id,
                        Reason: "Decay"
                    ));
                }

                await _particleRepository.UpdateAsync(particle);
                decayedCount++;
            }
        }

        _logger.LogInformation(
            "Processed decay for {Count} particles at {Time}", 
            decayedCount, 
            DateTime.UtcNow
        );
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error during particle decay processing");
        throw;
    }
}
```

**Decay Timeline:**
```
Hour 0:  LastInputAt = now, DecayLevel = 0%, State = Active
Hour 24: No input → State = Decaying, DecayLevel = 10%
Hour 25: DecayLevel = 20%
Hour 26: DecayLevel = 30%
...
Hour 33: DecayLevel = 100%, State = Expired
```

---

### 3. Expired Particle Cleanup

**Schedule:** Weekly (Sundays at 2 AM UTC)  
**Queue:** maintenance  
**Duration:** ~10-60 seconds

**Purpose:**
- Delete expired particles older than 30 days
- Free up database space
- Archive historical data (optional)

**Implementation:**
```csharp
public async Task CleanupExpiredParticlesAsync()
{
    try
    {
        _logger.LogInformation("Starting cleanup of expired particles at {Time}", DateTime.UtcNow);
        
        var allParticles = await _particleRepository.GetAllAsync();
        var expiredParticles = allParticles.Where(p => p.State == ParticleState.Expired);
        var cleanupThreshold = DateTime.UtcNow.AddDays(-30);
        var deletedCount = 0;

        foreach (var particle in expiredParticles)
        {
            if (particle.LastUpdatedAt < cleanupThreshold)
            {
                // Optional: Archive before delete
                // await _archiveService.ArchiveParticleAsync(particle);
                
                await _particleRepository.DeleteAsync(particle.Id);
                deletedCount++;
            }
        }

        _logger.LogInformation(
            "Cleaned up {Count} expired particles at {Time}", 
            deletedCount, 
            DateTime.UtcNow
        );
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error during expired particle cleanup");
        throw;
    }
}
```

---

## Job Scheduling

### Cron Expressions

**Common Patterns:**
```csharp
Cron.Minutely()                      // Every minute
Cron.Hourly()                        // Every hour at :00
Cron.Daily()                         // Every day at midnight
Cron.Daily(13, 30)                   // Every day at 1:30 PM
Cron.Weekly()                        // Every Sunday at midnight
Cron.Weekly(DayOfWeek.Monday)        // Every Monday at midnight
Cron.Monthly()                       // First day of month at midnight
Cron.Yearly()                        // January 1st at midnight
```

**Custom Cron:**
```csharp
// Every 15 minutes
"*/15 * * * *"

// Every day at 6 AM and 6 PM
"0 6,18 * * *"

// Weekdays at 9 AM
"0 9 * * 1-5"

// Every 5 minutes between 8 AM and 5 PM
"*/5 8-17 * * *"
```

**Cron Format:**
```
* * * * *
│ │ │ │ │
│ │ │ │ └─── Day of week (0-7, 0 or 7 = Sunday)
│ │ │ └───── Month (1-12)
│ │ └─────── Day of month (1-31)
│ └───────── Hour (0-23)
└─────────── Minute (0-59)
```

---

## Job Queues

### Queue Configuration

```csharp
builder.Services.AddHangfireServer(options =>
{
    options.Queues = new[] { "critical", "simulation", "default", "maintenance" };
});
```

**Queue Priority (processed in order):**
1. **critical** - Urgent operations (manual triggers, failures)
2. **simulation** - Core simulation jobs (tick, decay)
3. **default** - Standard background tasks
4. **maintenance** - Low-priority cleanup jobs

### Assigning Jobs to Queues

```csharp
// Critical queue
BackgroundJob.Enqueue<SimulationJobs>(
    "critical",
    job => job.ProcessDailyTickAsync()
);

// Maintenance queue
RecurringJob.AddOrUpdate<SimulationJobs>(
    "weekly-cleanup",
    job => job.CleanupExpiredParticlesAsync(),
    Cron.Weekly(),
    new RecurringJobOptions { QueueName = "maintenance" }
);
```

---

## Error Handling & Retries

### Automatic Retries

**Default Retry Policy:** 10 attempts with exponential backoff

```csharp
[AutomaticRetry(Attempts = 10, OnAttemptsExceeded = AttemptsExceededAction.Fail)]
public async Task ProcessDailyTickAsync()
{
    // If this throws, Hangfire will retry automatically
    await _simulationService.ProcessSimulationTickAsync();
}
```

**Custom Retry Policy:**
```csharp
[AutomaticRetry(Attempts = 5, DelaysInSeconds = new[] { 10, 30, 60, 300, 600 })]
public async Task ProcessParticleDecayAsync()
{
    // Retry with specific delays: 10s, 30s, 1m, 5m, 10m
    await _particleRepository.UpdateAsync(particle);
}
```

---

### Manual Retry

```csharp
try
{
    await _simulationService.ProcessSimulationTickAsync();
}
catch (Exception ex)
{
    _logger.LogError(ex, "Tick failed, scheduling retry in 5 minutes");
    
    BackgroundJob.Schedule<SimulationJobs>(
        job => job.ProcessDailyTickAsync(),
        TimeSpan.FromMinutes(5)
    );
    
    throw; // Still let Hangfire know it failed
}
```

---

### Dead Letter Queue

**Failed jobs** after all retries go to "Failed" state in dashboard.

**Manual Recovery:**
1. Open Hangfire dashboard (`/hangfire`)
2. Navigate to "Failed Jobs"
3. Inspect error details
4. Click "Requeue" to retry
5. Or "Delete" to discard

---

## Monitoring & Observability

### Hangfire Dashboard

**URL:** http://localhost:5004/hangfire

**Features:**
- Job history (succeeded/failed/processing)
- Real-time updates
- Retry failed jobs
- Delete jobs
- Trigger recurring jobs manually
- View job details and exceptions

**Screenshots:**
```
┌──────────────────────────────────────┐
│ Hangfire Dashboard                   │
├──────────────────────────────────────┤
│ Recurring Jobs (3)                   │
│  ✓ daily-simulation-tick   Next: 23h│
│  ✓ hourly-particle-decay   Next: 45m│
│  ✓ weekly-cleanup          Next: 4d │
├──────────────────────────────────────┤
│ Processing Jobs (1)                  │
│  ⟳ ProcessDailyTickAsync     15s    │
├──────────────────────────────────────┤
│ Succeeded Jobs (342)                 │
│ Failed Jobs (2)                      │
│ Scheduled Jobs (0)                   │
└──────────────────────────────────────┘
```

---

### Application Logging

**Structured Logging:**
```csharp
_logger.LogInformation(
    "Job {JobName} started at {StartTime} for {ParticleCount} particles",
    "ProcessDailyTick",
    DateTime.UtcNow,
    particleCount
);

_logger.LogInformation(
    "Job {JobName} completed in {Duration}ms",
    "ProcessDailyTick",
    stopwatch.ElapsedMilliseconds
);
```

---

### Performance Metrics

**Track Job Duration:**
```csharp
public async Task ProcessDailyTickAsync()
{
    var stopwatch = Stopwatch.StartNew();
    
    try
    {
        await _simulationService.ProcessSimulationTickAsync();
        
        stopwatch.Stop();
        _logger.LogInformation(
            "Daily tick completed in {Duration}ms",
            stopwatch.ElapsedMilliseconds
        );
    }
    catch (Exception ex)
    {
        stopwatch.Stop();
        _logger.LogError(ex, 
            "Daily tick failed after {Duration}ms",
            stopwatch.ElapsedMilliseconds
        );
        throw;
    }
}
```

---

## Job Registration (Startup)

### Configure Jobs on Application Start

```csharp
// Program.cs
var app = builder.Build();

// Configure Hangfire recurring jobs
using (var scope = app.Services.CreateScope())
{
    var recurringJobManager = scope.ServiceProvider.GetRequiredService<IRecurringJobManager>();

    // Daily simulation tick (midnight UTC)
    recurringJobManager.AddOrUpdate<SimulationJobs>(
        "daily-simulation-tick",
        job => job.ProcessDailyTickAsync(),
        Cron.Daily(0, 0),
        new RecurringJobOptions
        {
            TimeZone = TimeZoneInfo.Utc,
            QueueName = "simulation"
        }
    );

    // Hourly decay check
    recurringJobManager.AddOrUpdate<SimulationJobs>(
        "hourly-particle-decay",
        job => job.ProcessParticleDecayAsync(),
        Cron.Hourly(),
        new RecurringJobOptions
        {
            TimeZone = TimeZoneInfo.Utc,
            QueueName = "simulation"
        }
    );

    // Weekly cleanup (Sundays at 2 AM UTC)
    recurringJobManager.AddOrUpdate<SimulationJobs>(
        "weekly-cleanup",
        job => job.CleanupExpiredParticlesAsync(),
        Cron.Weekly(DayOfWeek.Sunday, 2, 0),
        new RecurringJobOptions
        {
            TimeZone = TimeZoneInfo.Utc,
            QueueName = "maintenance"
        }
    );
}

app.Run();
```

---

## Testing Background Jobs

### Manual Trigger via API

```csharp
[HttpPost("trigger/daily-tick")]
[Authorize(Roles = "Admin")]
public IActionResult TriggerDailyTick()
{
    BackgroundJob.Enqueue<SimulationJobs>(job => 
        job.ProcessDailyTickAsync()
    );
    
    return Ok(new { message = "Daily tick triggered" });
}
```

### Unit Testing

```csharp
[Fact]
public async Task ProcessDailyTick_UpdatesAllActiveParticles()
{
    // Arrange
    var mockService = new Mock<ISimulationService>();
    var job = new SimulationJobs(mockService.Object, ...);

    // Act
    await job.ProcessDailyTickAsync();

    // Assert
    mockService.Verify(s => s.ProcessSimulationTickAsync(), Times.Once);
}
```

---

## Best Practices

### Do's ✅

- Use descriptive job names
- Log job start and completion
- Set appropriate retry policies
- Use queues to prioritize jobs
- Monitor job failures in dashboard
- Handle exceptions gracefully
- Use cancellation tokens for long-running jobs
- Keep jobs idempotent (safe to retry)

### Don'ts ❌

- Don't run jobs synchronously in HTTP requests
- Don't ignore job failures
- Don't use jobs for real-time operations
- Don't store job state in memory
- Don't run CPU-intensive operations without queueing
- Don't forget to test job logic separately
- Don't use infinite retries

---

## Scalability

### Horizontal Scaling

**Run multiple Hangfire servers:**
```yaml
# docker-compose.yml
simulation-engine-1:
  image: simulation-engine:latest
  environment:
    HANGFIRE_SERVER_NAME: SimEngine-1

simulation-engine-2:
  image: simulation-engine:latest
  environment:
    HANGFIRE_SERVER_NAME: SimEngine-2
```

**Load Balancing:**
- Hangfire distributes jobs across servers
- Only one server processes each job
- Uses SQL Server locks for coordination

---

### Worker Count Optimization

```csharp
builder.Services.AddHangfireServer(options =>
{
    // CPU-bound jobs: 1x cores
    // I/O-bound jobs: 2-4x cores
    options.WorkerCount = Environment.ProcessorCount * 2;
});
```

---

## Future Enhancements

- [ ] Job batches (process multiple related jobs)
- [ ] Job continuations with conditions
- [ ] Priority queues for critical jobs
- [ ] Job progress tracking
- [ ] Email notifications on job failure
- [ ] Dashboard custom widgets
- [ ] Job execution metrics/analytics
- [ ] Distributed caching of job data
