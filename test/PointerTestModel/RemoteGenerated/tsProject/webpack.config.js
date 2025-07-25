const path = require('path');
module.exports = {
  entry: './src/app.ts',
  output: {
    filename: 'bundle.js',
    path: path.resolve(__dirname, 'wwwroot'),
    clean: false,
  },
  resolve: { extensions: ['.ts', '.js'] },
  module: { rules: [{ test: /\.ts$/, use: 'ts-loader', exclude: /node_modules/ }] },
  devtool: 'source-map',
  devServer: { static: { directory: path.join(__dirname, 'wwwroot') }, hot: true, open: true, port: 3000 },
  mode: 'development'
};
