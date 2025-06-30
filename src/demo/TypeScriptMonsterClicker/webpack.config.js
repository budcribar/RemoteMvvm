const path = require('path');

module.exports = {
    entry: './wwwroot/app.ts',
  output: {
    filename: 'bundle.js',
    path: path.resolve(__dirname, 'wwwroot'),
    clean: true,
  },
  resolve: {
    extensions: ['.tsx', '.ts', '.js'],
  },
  module: {
    rules: [
      {
        test: /\.tsx?$/,
        use: 'ts-loader',
        exclude: /node_modules/,
      },
      {
        test: /\.css$/i,
        use: ['style-loader', 'css-loader'],
      },
      {
        test: /\.(png|svg|jpg|jpeg|gif)$/i,
        type: 'asset/resource',
      },
    ],
  },
  devtool: 'source-map',
  devServer: {
    static: './dist',
    hot: true,
    open: true,
    port: 3000,
  },
  mode: 'development',
};