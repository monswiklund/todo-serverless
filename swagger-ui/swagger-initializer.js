window.onload = function() {
    // AUTO-REPLACED BY GITHUB ACTIONS
    const API_URL = 'PLACEHOLDER_API_URL';

    window.ui = SwaggerUIBundle({
        url: `${API_URL}swagger/v1/swagger.json`,
        dom_id: '#swagger-ui',
        deepLinking: true,
        presets: [
            SwaggerUIBundle.presets.apis,
            SwaggerUIStandalonePreset
        ],
        plugins: [
            SwaggerUIBundle.plugins.DownloadUrl
        ],
        layout: "StandaloneLayout"
    });
};