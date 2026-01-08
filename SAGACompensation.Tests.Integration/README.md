# SAGA Compensation Integration Tests

Dette projekt indeholder integration tests for SAGA compensation flow implementeringen.

## Test Struktur

### Test 1: Partial Failure Scenario
**Fil:** `Test1_PartialFailureScenario.cs`

Tester scenarier hvor:
- Ordre oprettes med flere sælgere
- En sælger fejler (f.eks. WarehouseService)
- Compensation udløses og succesfulde items rulles tilbage

**Test Cases:**
- `PartialFailure_WhenSecondSellerFails_ShouldCompensateFirstSeller` - Tester at første sælger kompenseres når anden fejler
- `PartialFailure_WhenMultipleItemsFail_ShouldCompensateAllSuccessfulItems` - Tester kompensation af alle succesfulde items når flere fejler

### Test 2: Retry Mechanism Test
**Fil:** `Test2_RetryMechanismTest.cs`

Tester retry mekanismen:
- Exponential backoff (2ⁿ sekunder)
- Max 3 retry forsøg
- DLQ efter max retries

**Test Cases:**
- `RetryMechanism_WhenTransientErrorOccurs_ShouldRetryWithExponentialBackoff` - Tester retry med exponential backoff
- `RetryMechanism_WhenPermanentErrorOccurs_ShouldPublishFailureEvent` - Tester at permanent fejl publiserer failure event

### Test 3: Compensation Orchestrator
**Fil:** `Test3_CompensationOrchestratorTest.cs`

Tester CompensationService orchestration:
- Modtagelse af failure events
- Publikation af CompensationRequired events
- Koordinering af rollback

**Test Cases:**
- `CompensationOrchestrator_WhenInventoryReservationFails_ShouldPublishCompensationRequired` - Tester at CompensationService modtager og håndterer failure events
- `CompensationOrchestrator_WhenMultipleFailuresOccur_ShouldAggregateInCompensationRequired` - Tester aggregation af flere failures
- `CompensationOrchestrator_WhenNotificationFails_ShouldNotTriggerCompensation` - Tester at notification failures ikke trigger compensation

### Test 4: End-to-End Flow
**Fil:** `Test4_EndToEndFlowTest.cs`

Tester komplet flow:
- Succesfuldt checkout flow
- Fejl i midten af flow
- Sammenligning af statuses

**Test Cases:**
- `EndToEndFlow_WhenCheckoutSucceeds_ShouldMarkItemsAsFulfilled` - Tester succesfuldt flow
- `EndToEndFlow_WhenCheckoutFails_ShouldCompensateAndMarkAsCompensated` - Tester fejl flow med compensation
- `EndToEndFlow_CompareSuccessVsFailure_ShouldShowDifferentStatuses` - Sammenligner succes vs fejl scenarier

## Test Data

### TestDataBuilders
**Fil:** `TestData/TestDataBuilders.cs`

Helper klasser til at oprette test data:
- `CreateMultiSellerOrder` - Opretter ordre med flere sælgere
- `CreateOrderPaidEvent` - Opretter OrderPaid event
- `CreateWarehouseItems` - Opretter warehouse items
- `CreateInventoryReservationFailedEvent` - Opretter failure events
- `CreateCompensationRequiredEvent` - Opretter compensation events

### MockRabbitMQEvents
**Fil:** `TestData/MockRabbitMQEvents.cs`

Helper til at publishere og consumere RabbitMQ events i tests.

## Kørsel af Tests

### Kør alle tests
```bash
dotnet test SAGACompensation.Tests.Integration
```

### Kør specifik test
```bash
dotnet test SAGACompensation.Tests.Integration --filter "FullyQualifiedName~Test1_PartialFailureScenario"
```

### Kør med detaljeret output
```bash
dotnet test SAGACompensation.Tests.Integration --verbosity detailed
```

## Forudsætninger

Tests bruger **eksisterende docker-compose services** (ikke Testcontainers):
- SQL Server på `localhost:1433` (fra docker-compose)
- RabbitMQ på `localhost:5672` (fra docker-compose)
- Redis på `localhost:6379` (hvis nødvendigt)

**VIGTIGT:** Docker-compose services skal være kørende før tests køres:
```bash
docker-compose up -d
```

Tests tjekker automatisk om services er tilgængelige og fejler hurtigt (5 sekunder timeout) hvis de ikke er.

**Fordele ved denne tilgang:**
- ✅ Meget hurtigere (ingen container startup - tests kører under 30 sekunder)
- ✅ Bruger samme services som development
- ✅ Ingen Testcontainers overhead

## Test Coverage

Tests dækker:
- ✅ Partial failure scenarier
- ✅ Retry mechanism med exponential backoff
- ✅ DLQ funktionalitet
- ✅ Compensation orchestrator
- ✅ End-to-end flows
- ✅ Event publishing og consumption
- ✅ Status tracking (Fulfilled vs Compensated)

## Noter

- Tests bruger Testcontainers for at isolere test miljøet
- Tests kan tage længere tid at køre pga. container startup
- Nogle tests kræver faktiske service instanser eller mocking for fuld funktionalitet
- Event timing kan variere - tests inkluderer delays for at håndtere asynkron processing

