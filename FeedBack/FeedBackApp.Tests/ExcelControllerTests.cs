using Moq;
using FeedBackApp.Controllers;
using FeedBackApp.Exceptions;
using FeedBackApp.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using System.Security.Claims;

namespace FeedBackApp.Tests
{
    [TestFixture]
    public class ExcelControllerTests
    {
        private Mock<IExcelService> _excelSvcMock;
        private Mock<IAuditService> _auditSvcMock;
        private ExcelController     _controller;

        [SetUp]
        public void Setup()
        {
            _excelSvcMock = new Mock<IExcelService>();
            _auditSvcMock = new Mock<IAuditService>();

            _controller = new ExcelController(
                _excelSvcMock.Object,
                _auditSvcMock.Object,
                NullLogger<ExcelController>.Instance);

            SetUser(userId: 1, role: "Creator");
        }

        private void SetUser(int userId, string role)
        {
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim(ClaimTypes.Role, role)
            };
            var identity  = new ClaimsIdentity(claims, "Test");
            var principal = new ClaimsPrincipal(identity);

            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = principal }
            };
            _controller.ControllerContext.HttpContext.Items["CorrelationId"] = "test-corr-id";
        }

        private void SetNoUser()
        {
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity())
                }
            };
        }

        // ── Happy path ────────────────────────────────────────────────────────

        [Test]
        public async Task ExportExcel_ValidRequest_ReturnsFileContentResult()
        {
            _excelSvcMock.Setup(s => s.ExportExcelAsync(1, 1, "Creator"))
                .ReturnsAsync(new byte[] { 1, 2, 3 });

            var result = await _controller.ExportExcel(1);

            Assert.That(result, Is.InstanceOf<FileContentResult>());
        }

        [Test]
        public async Task ExportExcel_ValidRequest_ReturnsXlsxContentType()
        {
            _excelSvcMock.Setup(s => s.ExportExcelAsync(1, 1, "Creator"))
                .ReturnsAsync(new byte[] { 1, 2, 3 });

            var result = await _controller.ExportExcel(1);

            var file = (FileContentResult)result;
            Assert.That(file.ContentType,
                Is.EqualTo("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"));
        }

        [Test]
        public async Task ExportExcel_ValidRequest_FileNameContainsSurveyId()
        {
            _excelSvcMock.Setup(s => s.ExportExcelAsync(42, 1, "Creator"))
                .ReturnsAsync(new byte[] { 1 });

            var result = await _controller.ExportExcel(42);

            var file = (FileContentResult)result;
            Assert.That(file.FileDownloadName, Does.Contain("42"));
        }

        [Test]
        public async Task ExportExcel_ValidRequest_FileNameEndsWithXlsx()
        {
            _excelSvcMock.Setup(s => s.ExportExcelAsync(1, 1, "Creator"))
                .ReturnsAsync(new byte[] { 1 });

            var result = await _controller.ExportExcel(1);

            var file = (FileContentResult)result;
            Assert.That(file.FileDownloadName, Does.EndWith(".xlsx"));
        }

        [Test]
        public async Task ExportExcel_ValidRequest_ReturnsSameBytesFromService()
        {
            var expected = new byte[] { 10, 20, 30 };
            _excelSvcMock.Setup(s => s.ExportExcelAsync(1, 1, "Creator"))
                .ReturnsAsync(expected);

            var result = await _controller.ExportExcel(1);

            var file = (FileContentResult)result;
            Assert.That(file.FileContents, Is.EqualTo(expected));
        }

        // ── Error responses ───────────────────────────────────────────────────

        [Test]
        public async Task ExportExcel_SurveyNotFound_Returns404()
        {
            _excelSvcMock.Setup(s => s.ExportExcelAsync(99, 1, "Creator"))
                .ThrowsAsync(new NotFoundException("Survey 99 not found."));

            var result = await _controller.ExportExcel(99);

            Assert.That(result, Is.InstanceOf<NotFoundObjectResult>());
        }

        [Test]
        public async Task ExportExcel_Forbidden_Returns403()
        {
            _excelSvcMock.Setup(s => s.ExportExcelAsync(1, 1, "Creator"))
                .ThrowsAsync(new ForbiddenException("Access denied."));

            var result = await _controller.ExportExcel(1);

            var obj = (ObjectResult)result;
            Assert.That(obj.StatusCode, Is.EqualTo(403));
        }

        [Test]
        public async Task ExportExcel_BadRequest_Returns400()
        {
            _excelSvcMock.Setup(s => s.ExportExcelAsync(1, 1, "Creator"))
                .ThrowsAsync(new BadRequestException("No version."));

            var result = await _controller.ExportExcel(1);

            Assert.That(result, Is.InstanceOf<BadRequestObjectResult>());
        }

        [Test]
        public async Task ExportExcel_UnexpectedException_Returns500()
        {
            _excelSvcMock.Setup(s => s.ExportExcelAsync(1, 1, "Creator"))
                .ThrowsAsync(new InvalidOperationException("Boom"));

            var result = await _controller.ExportExcel(1);

            var obj = (ObjectResult)result;
            Assert.That(obj.StatusCode, Is.EqualTo(500));
        }

        [Test]
        public async Task ExportExcel_MissingUserClaim_Returns401()
        {
            SetNoUser();

            var result = await _controller.ExportExcel(1);

            Assert.That(result, Is.InstanceOf<UnauthorizedObjectResult>());
        }

        // ── Admin role ────────────────────────────────────────────────────────

        [Test]
        public async Task ExportExcel_AdminRole_PassesAdminRoleToService()
        {
            SetUser(userId: 5, role: "Admin");
            _excelSvcMock.Setup(s => s.ExportExcelAsync(1, 5, "Admin"))
                .ReturnsAsync(new byte[] { 1 });

            var result = await _controller.ExportExcel(1);

            Assert.That(result, Is.InstanceOf<FileContentResult>());
            _excelSvcMock.Verify(s => s.ExportExcelAsync(1, 5, "Admin"), Times.Once);
        }
    }
}
