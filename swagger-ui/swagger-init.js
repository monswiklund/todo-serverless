window.onload = function() {
    // ERSÄTT med din API Gateway URL efter deploy
    const API_URL = 'https://YOUR_API_GATEWAY_ID.execute-api.eu-north-1.amazonaws.com/Prod';

    window.ui = SwaggerUIBundle({
        url: `${API_URL}/swagger/v1/swagger.json`,
        dom_id: '#swagger-ui',
        deepLinking: true,
        presets: [
            SwaggerUIBundle.presets.apis,
            SwaggerUIStandalonePreset
        ],
        layout: "StandaloneLayout"
    });
};