using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Dinolab;
using Dinolab.Models.DTOs.Requests;
using Dinolab.Models.DTOs.Responses;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

// [Route("[controller]")] // api/authmanagement
[ApiController]
public class AuthManagementController : ControllerBase
{
    private readonly UserManager<IdentityUser> _userManager;
    private readonly JwtConfig _jwtConfig;
    public AuthManagementController(UserManager<IdentityUser> userManager, IOptionsMonitor<JwtConfig> optionsMonitor)
    {
        _userManager = userManager;
        _jwtConfig = optionsMonitor.CurrentValue;
    }

    [HttpPost]
    [Route("Register")]
    public async Task<IActionResult> Register([FromBody] UserRegistrationRequestDTO user)
    {
        // Check if the incoming request is valid
        if (ModelState.IsValid)
        {
            // check i the user with the same email exist
            var existingEmail = await _userManager.FindByEmailAsync(user.Email);
            var existingUser = await _userManager.FindByNameAsync(user.Username);

            if (existingEmail != null)
            {
                return BadRequest(new RegistrationResponses()
                {
                    Success = false,
                    Errors = new List<string>(){
                                            "Email already exist"
                                        }
                });
            }
            if(existingUser != null)
            {
                return BadRequest(new RegistrationResponses()
                {
                    Success = false,
                    Errors = new List<string>(){
                                            "UserName already exist"
                                        }
                });
            }

            var newUser = new IdentityUser() { Email = user.Email, UserName = user.Username };
            var isCreated = await _userManager.CreateAsync(newUser, user.Password);
            if (isCreated.Succeeded)
            {
                await _userManager.AddToRoleAsync(newUser, "User");
                var jwtToken = GenerateJwtToken(newUser);
                return Ok(new RegistrationResponses()
                {
                    Success = true,
                    Token = jwtToken
                });
            }

            return new JsonResult(new RegistrationResponses()
            {
                Success = false,
                Errors = isCreated.Errors.Select(x => x.Description).ToList()
            }
                    )
            { StatusCode = 500 };
        }

        return BadRequest(new RegistrationResponses()
        {
            Success = false,
            Errors = new List<string>(){
                                            "Invalid payload"
                                        }
        });
    }

    [HttpPost]
    [Route("Login")]
    public async Task<IActionResult> Login([FromBody] UserLoginRequest user)
    {
        if (ModelState.IsValid)
        {
            // check if the user with the same email exist
            var existingUser = await _userManager.FindByEmailAsync(user.Email);

            if (existingUser == null)
            {
                // We dont want to give to much information on why the request has failed for security reasons
                return BadRequest(new RegistrationResponses()
                {
                    Success = false,
                    Errors = new List<string>(){
                                        "Invalid Email or Password"
                                    }
                });
            }

            // Now we need to check if the user has inputed the right password
            var isCorrect = await _userManager.CheckPasswordAsync(existingUser, user.Password);

            if (isCorrect)
            {
                var jwtToken = GenerateJwtToken(existingUser);
                var email = user.Email;

                return Ok(new RegistrationResponses()
                {
                    Success = true,
                    Token = jwtToken
                });
            }
            else
            {
                // We dont want to give to much information on why the request has failed for security reasons
                return BadRequest(new RegistrationResponses()
                {
                    Success = false,
                    Errors = new List<string>(){
                                         "Invalid Email or Password"
                                    }
                });
            }
        }

        return BadRequest(new RegistrationResponses()
        {
            Success = false,
            Errors = new List<string>(){
                                        "Invalid payload"
                                    }
        });
    }
    private string GenerateJwtToken(IdentityUser user)
    {
        // Now its ime to define the jwt token which will be responsible of creating our tokens
        var jwtTokenHandler = new JwtSecurityTokenHandler();

        // We get our secret from the appsettings
        var key = Encoding.ASCII.GetBytes(_jwtConfig.Secret);
        var userRole = _userManager.GetRolesAsync(user);


        // we define our token descriptor
        // We need to utilise claims which are properties in our token which gives information about the token
        // which belong to the specific user who it belongs to
        // so it could contain their id, name, email the good part is that these information
        // are generated by our server and identity framework which is valid and trusted
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[]
            {
                new Claim("Id", user.Id),
                new Claim(JwtRegisteredClaimNames.Sub, user.UserName),
                new Claim(JwtRegisteredClaimNames.Email, user.Email),
                // the JTI is used for our refresh token which we will be convering in the next video
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            }),
            // the life span of the token needs to be shorter and utilise refresh token to keep the user signedin
            // but since this is a demo app we can extend it to fit our current need
            Expires = DateTime.UtcNow.AddMinutes(20),
            // here we are adding the encryption alogorithim information which will be used to decrypt our token
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha512Signature)
        };
        foreach (var role in userRole.Result)
        {
            var claim = new Claim(ClaimTypes.Role, role);
            tokenDescriptor.Subject.AddClaim(claim);
        }

        var token = jwtTokenHandler.CreateToken(tokenDescriptor);

        var jwtToken = jwtTokenHandler.WriteToken(token);

        return jwtToken;
    }
}